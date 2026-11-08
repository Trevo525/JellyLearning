// E-Learning course UI for jellyfin-web, injected into index.html by the File Transformation plugin.
// Adds course progress, the full outline (including non-video lectures), resources and a file viewer
// to the details pages of courses (series), sections (seasons) and lectures (episodes).
//
// All jellyfin-web specifics are in the `web` object below, so a web client update that breaks the
// UI should only need changes there. Anything unexpected makes the UI do nothing, never break a page.
(function () {
    'use strict';

    if (window.__elearning) {
        return;
    }
    window.__elearning = true;

    // ---- jellyfin-web integration (selectors and globals) -------------------------------------

    const web = {
        api: function () { return window.ApiClient; },
        detailPage: function (target) {
            return target && target.id === 'itemDetailPage' ? target : null;
        },
        // Where the panel goes: in the content column under the overview, above "Next Up" and the
        // seasons/episodes list. Falls back to the top of the full-width area.
        mount: function (page, panel) {
            const nextUp = page.querySelector('.detailPagePrimaryContent .nextUpSection');
            if (nextUp && nextUp.parentNode) {
                nextUp.parentNode.insertBefore(panel, nextUp);
                return true;
            }
            const fallback = page.querySelector('.detailPageSecondaryContainer');
            if (fallback) {
                fallback.prepend(panel);
                return true;
            }
            return false;
        },
        itemIdFromUrl: function () {
            const query = (window.location.hash.split('?')[1] || window.location.search.slice(1) || '');
            return new URLSearchParams(query).get('id');
        },
        // The course player page, relative to jellyfin-web's <base>/web/.
        playerHref: function (seriesId, lectureId) {
            return '../ELearning/Web/player.html#' + encodeURIComponent(seriesId) + (lectureId ? '/' + encodeURIComponent(lectureId) : '');
        },
        // jellyfin-web's own headings on course pages, in course words. It writes "Seasons" /
        // "Episodes" after loading the children, so they are set again whenever it does.
        headings: {
            course: { '#childrenCollapsible .sectionTitle > span': 'Sections', '.nextUpSection .sectionTitle': 'Next lecture' },
            section: { '#childrenCollapsible .sectionTitle > span': 'Lectures', '.nextUpSection .sectionTitle': 'Next lecture' }
        },
        relabel: function (page, kind) {
            const labels = web.headings[kind];
            if (!labels) {
                return null;
            }
            const apply = function () {
                Object.keys(labels).forEach(function (selector) {
                    page.querySelectorAll(selector).forEach(function (el) {
                        if (el.textContent.trim() !== labels[selector]) {
                            el.textContent = labels[selector];
                        }
                    });
                });
            };
            apply();
            const observer = new MutationObserver(apply);
            observer.observe(page, { childList: true, subtree: true, characterData: true });
            return observer;
        },
        detailsHref: function (itemId) {
            const api = web.api();
            return '#/details?id=' + encodeURIComponent(itemId) + (api ? '&serverId=' + encodeURIComponent(api.serverId()) : '');
        }
    };

    // ---- API ------------------------------------------------------------------------------------

    function authHeaders() {
        return { Authorization: 'MediaBrowser Token="' + web.api().accessToken() + '"' };
    }

    function apiUrl(path) {
        return web.api().getUrl('ELearning/' + path);
    }

    function request(path, options) {
        return fetch(apiUrl(path), Object.assign({ headers: authHeaders() }, options || {}));
    }

    const notCourses = new Set();

    async function getOutline(itemId) {
        if (notCourses.has(itemId)) {
            return null;
        }
        const response = await request('Courses/' + encodeURIComponent(itemId) + '/Outline');
        if (response.status === 404) {
            notCourses.add(itemId);
            return null;
        }
        if (!response.ok) {
            throw new Error('Outline request failed: ' + response.status);
        }
        return response.json();
    }

    function setComplete(lectureId, completed) {
        return request('Lectures/' + encodeURIComponent(lectureId) + '/Complete', { method: completed ? 'POST' : 'DELETE' });
    }

    // Downloads are attachments (never rendered), so the token may go in the URL, as jellyfin-web does.
    function downloadUrl(fileId) {
        return apiUrl('Files/' + encodeURIComponent(fileId)) + '?download=true&api_key=' + encodeURIComponent(web.api().accessToken());
    }

    // ---- DOM helpers ------------------------------------------------------------------------------

    function h(tag, attrs) {
        const el = document.createElement(tag);
        for (const [key, value] of Object.entries(attrs || {})) {
            if (value === null || value === undefined || value === false) {
                continue;
            }
            if (key === 'class') {
                el.className = value;
            } else if (key === 'text') {
                el.textContent = value;
            } else if (key.startsWith('on')) {
                el.addEventListener(key.slice(2), value);
            } else {
                el.setAttribute(key, value === true ? '' : value);
            }
        }
        for (let i = 2; i < arguments.length; i++) {
            const child = arguments[i];
            if (child === null || child === undefined || child === false) {
                continue;
            }
            el.append(child);
        }
        return el;
    }

    function icon(name, extra) {
        return h('span', { class: 'material-icons el-icon ' + name + (extra ? ' ' + extra : ''), 'aria-hidden': 'true' });
    }

    function button(iconName, label, onclick, extra) {
        return h('button', { type: 'button', class: 'el-button ' + (extra || ''), title: label, 'aria-label': label, onclick: onclick }, icon(iconName));
    }

    function formatDuration(ticks) {
        if (!ticks) {
            return '';
        }
        const total = Math.round(ticks / 1e7);
        const hours = Math.floor(total / 3600);
        const minutes = Math.floor((total % 3600) / 60);
        const seconds = total % 60;
        const mm = hours ? String(minutes).padStart(2, '0') : String(minutes);
        return (hours ? hours + ':' : '') + mm + ':' + String(seconds).padStart(2, '0');
    }

    function formatSize(bytes) {
        if (bytes === undefined || bytes === null) {
            return 'missing';
        }
        const units = ['B', 'KB', 'MB', 'GB'];
        let size = bytes;
        let unit = 0;
        while (size >= 1024 && unit < units.length - 1) {
            size /= 1024;
            unit++;
        }
        return (unit ? size.toFixed(size < 10 ? 1 : 0) : size) + ' ' + units[unit];
    }

    // ---- Kinds --------------------------------------------------------------------------------------

    const viewable = new Set(['Pdf', 'Html', 'Markdown', 'Text', 'Image']);

    function isQuiz(title) {
        return /\bquiz\b/i.test(title || '');
    }

    function kindIcon(kind, title, externalUrl) {
        if (externalUrl) {
            return 'link';
        }
        switch (kind) {
            case 'Video': return 'play_circle';
            case 'Html': return isQuiz(title) ? 'quiz' : 'article';
            case 'Pdf': return 'picture_as_pdf';
            case 'Markdown':
            case 'Text': return 'description';
            case 'Image': return 'image';
            case 'Archive': return 'folder_zip';
            case 'Document': return 'table_chart';
            case 'Link': return 'link';
            default: return 'insert_drive_file';
        }
    }

    // ---- Progress -----------------------------------------------------------------------------------

    function progressBar(progress, label) {
        return h('div', { class: 'el-progress' },
            h('div', { class: 'el-progress-text' },
                h('span', { text: label }),
                h('span', { text: progress.Completed + ' / ' + progress.Total + ' lectures • ' + progress.Percent + '%' })),
            h('div', { class: 'el-progress-track', role: 'progressbar', 'aria-valuemin': '0', 'aria-valuemax': '100', 'aria-valuenow': String(progress.Percent) },
                h('div', { class: 'el-progress-fill', style: 'width:' + progress.Percent + '%' })));
    }

    function allLectures(outline) {
        const list = [];
        outline.Sections.forEach(function (section) {
            section.Lectures.forEach(function (lecture) { list.push({ section: section, lecture: lecture }); });
        });
        return list;
    }

    // The lecture to continue with: a video with a resume position, else the first open lecture.
    function nextLecture(outline) {
        const lectures = allLectures(outline);
        return lectures.find(function (x) { return !x.lecture.Completed && x.lecture.PositionTicks > 0; })
            || lectures.find(function (x) { return !x.lecture.Completed; })
            || null;
    }

    // ---- Opening things -----------------------------------------------------------------------------

    // The series being shown, for links into the course player.
    let activeSeriesId = null;

    function openFile(file, refresh) {
        if (file.ExternalUrl) {
            window.open(file.ExternalUrl, '_blank', 'noopener,noreferrer');
        } else if (viewable.has(file.Kind)) {
            openViewer(file, null, refresh);
        } else {
            window.location.href = downloadUrl(file.Id);
        }
    }

    // ---- Viewer -------------------------------------------------------------------------------------

    function openViewer(file, lecture, refresh) {
        const objectUrls = [];
        let changed = false;
        const body = h('div', { class: 'el-viewer-body' }, h('div', { class: 'el-viewer-message', text: 'Loading…' }));

        let completeButton = null;
        function renderComplete(done) {
            completeButton.classList.toggle('el-done', done);
            completeButton.title = done ? 'Completed – click to mark as not completed' : 'Mark as completed';
            completeButton.setAttribute('aria-label', completeButton.title);
            completeButton.setAttribute('aria-pressed', String(done));
            completeButton.firstChild.className = 'material-icons el-icon ' + (done ? 'check_circle' : 'radio_button_unchecked');
        }
        if (lecture) {
            let done = true;
            completeButton = button('check_circle', '', function () {
                done = !done;
                renderComplete(done);
                changed = true;
                setComplete(lecture.Id, done);
            }, 'el-complete');
            // Opening a document lecture completes it, like reading the page on the course site.
            renderComplete(true);
            if (!lecture.Completed) {
                changed = true;
                setComplete(lecture.Id, true);
            }
        }

        function close() {
            overlay.remove();
            document.removeEventListener('keydown', onKey, true);
            objectUrls.forEach(URL.revokeObjectURL);
            if (changed && refresh) {
                refresh();
            }
        }
        function onKey(e) {
            if (e.key === 'Escape') {
                e.stopPropagation();
                e.preventDefault();
                close();
            }
        }

        const overlay = h('div', { class: 'el-viewer', role: 'dialog', 'aria-modal': 'true', 'aria-label': file.Name },
            h('div', { class: 'el-viewer-header' },
                icon(kindIcon(file.Kind, file.Name)),
                h('h2', { class: 'el-viewer-title', text: file.Name }),
                completeButton,
                h('a', { class: 'el-button', href: downloadUrl(file.Id), title: 'Download', 'aria-label': 'Download' }, icon('download')),
                button('close', 'Close', close)),
            body);
        document.body.append(overlay);
        document.addEventListener('keydown', onKey, true);
        overlay.querySelector('.el-viewer-header button:last-child').focus();

        loadInto(body, file, objectUrls).catch(function (err) {
            body.replaceChildren(h('div', { class: 'el-viewer-message' },
                h('p', { text: 'This file can’t be shown here (' + err.message + ').' }),
                h('a', { class: 'el-link', href: downloadUrl(file.Id), text: 'Download it instead' })));
        });
    }

    async function loadInto(body, file, objectUrls) {
        const id = encodeURIComponent(file.Id);
        if (file.Kind === 'Html' || file.Kind === 'Markdown' || file.Kind === 'Text') {
            // Fetched with the token in a header; the page never sees it. No allow-same-origin, so even
            // scripts in HTML quizzes can't reach the Jellyfin session.
            const response = await request('Files/' + id + '/Render');
            if (!response.ok) {
                throw new Error('HTTP ' + response.status);
            }
            let html = await response.text();
            if (!/<base\b/i.test(html)) {
                html = /<head[^>]*>/i.test(html) ? html.replace(/<head[^>]*>/i, '$&<base target="_blank">') : '<base target="_blank">' + html;
            }
            const sandbox = (file.Kind === 'Html' ? 'allow-scripts ' : '') + 'allow-popups allow-popups-to-escape-sandbox';
            body.replaceChildren(h('iframe', { class: 'el-frame el-frame-document', sandbox: sandbox, srcdoc: html, title: file.Name, referrerpolicy: 'no-referrer' }));
            return;
        }

        const response = await request('Files/' + id);
        if (!response.ok) {
            throw new Error('HTTP ' + response.status);
        }
        const blob = await response.blob();
        const url = URL.createObjectURL(file.Kind === 'Pdf' ? new Blob([blob], { type: 'application/pdf' }) : blob);
        objectUrls.push(url);
        body.replaceChildren(file.Kind === 'Image'
            ? h('div', { class: 'el-image' }, h('img', { src: url, alt: file.Name }))
            : h('iframe', { class: 'el-frame', src: url, title: file.Name }));
    }

    // ---- Panels ---------------------------------------------------------------------------------------

    function fileRow(file, refresh) {
        const actions = h('span', { class: 'el-actions' });
        if (file.ExternalUrl) {
            actions.append(h('a', { class: 'el-button', href: file.ExternalUrl, target: '_blank', rel: 'noopener noreferrer', title: 'Open link', 'aria-label': 'Open link' }, icon('open_in_new')));
        } else {
            if (viewable.has(file.Kind)) {
                actions.append(button('visibility', 'View', function () { openFile(file, refresh); }));
            }
            actions.append(h('a', { class: 'el-button', href: downloadUrl(file.Id), title: 'Download', 'aria-label': 'Download' }, icon('download')));
        }
        return h('li', { class: 'el-file' },
            icon(kindIcon(file.Kind, file.Name, file.ExternalUrl)),
            h('button', { type: 'button', class: 'el-file-name', onclick: function () { openFile(file, refresh); }, text: file.Name }),
            h('span', { class: 'el-meta', text: file.ExternalUrl ? new URL(file.ExternalUrl).hostname : formatSize(file.Size) }),
            actions);
    }

    function fileList(title, files, refresh) {
        if (!files || !files.length) {
            return null;
        }
        return h('div', { class: 'el-block' },
            h('h3', { class: 'el-heading', text: title }),
            h('ul', { class: 'el-files' }, ...files.map(function (f) { return fileRow(f, refresh); })));
    }

    function lectureRow(lecture, options) {
        const refresh = options.refresh;
        const status = lecture.Kind === 'Video'
            ? icon(lecture.Completed ? 'check_circle' : 'radio_button_unchecked', 'el-status' + (lecture.Completed ? ' el-done' : ''))
            : button(lecture.Completed ? 'check_circle' : 'radio_button_unchecked', lecture.Completed ? 'Mark as not completed' : 'Mark as completed',
                function () { setComplete(lecture.Id, !lecture.Completed).then(refresh); }, 'el-status' + (lecture.Completed ? ' el-done' : ''));

        let meta = '';
        if (lecture.Kind === 'Video') {
            meta = lecture.PositionTicks > 0 && !lecture.Completed
                ? formatDuration(lecture.PositionTicks) + ' / ' + formatDuration(lecture.RunTimeTicks)
                : formatDuration(lecture.RunTimeTicks);
        } else if (lecture.ExternalUrl) {
            meta = 'Link';
        } else {
            meta = isQuiz(lecture.Title) ? 'Quiz' : lecture.Kind === 'Pdf' ? 'PDF' : 'Reading';
        }

        const title = h('a', { class: 'el-lecture-title', href: web.playerHref(activeSeriesId, lecture.Id), text: lecture.Title });

        const resume = lecture.Kind === 'Video' && lecture.PositionTicks > 0 && lecture.RunTimeTicks && !lecture.Completed
            ? h('div', { class: 'el-resume' }, h('div', { class: 'el-progress-fill', style: 'width:' + Math.min(100, 100 * lecture.PositionTicks / lecture.RunTimeTicks).toFixed(1) + '%' }))
            : null;

        // Attachments stay folded behind a paperclip toggle so long sections remain scannable.
        const attachments = lecture.Attachments.length
            ? h('ul', { class: 'el-files el-attachments', hidden: true }, ...lecture.Attachments.map(function (f) { return fileRow(f, refresh); }))
            : null;
        const attachmentToggle = attachments
            ? h('button', {
                type: 'button',
                class: 'el-meta el-attach-toggle',
                title: 'Show ' + lecture.Attachments.length + ' resource(s)',
                'aria-expanded': 'false',
                onclick: function () {
                    attachments.hidden = !attachments.hidden;
                    this.setAttribute('aria-expanded', String(!attachments.hidden));
                    this.title = (attachments.hidden ? 'Show ' : 'Hide ') + lecture.Attachments.length + ' resource(s)';
                }
            }, icon('attach_file'), String(lecture.Attachments.length))
            : null;

        return h('li', { class: 'el-lecture' + (options.current ? ' el-current' : '') + (lecture.Completed ? ' el-completed' : ''), 'aria-current': options.current ? 'true' : null },
            h('div', { class: 'el-lecture-row' },
                status,
                icon(kindIcon(lecture.Kind, lecture.Title, lecture.ExternalUrl), 'el-kind'),
                h('div', { class: 'el-lecture-main' }, title, resume),
                attachmentToggle,
                h('span', { class: 'el-meta', text: meta })),
            attachments);
    }

    function sectionBlock(section, options) {
        const lectures = h('ol', { class: 'el-lectures' }, ...section.Lectures.map(function (lecture) {
            return lectureRow(lecture, { refresh: options.refresh, current: lecture.ItemId && lecture.ItemId === options.currentItemId });
        }));
        const resources = fileList('Section resources', section.Resources, options.refresh);
        const summary = h('summary', { class: 'el-section-summary' },
            h('span', { class: 'el-section-title', text: section.Title }),
            h('span', { class: 'el-meta', text: section.Progress.Completed + ' / ' + section.Progress.Total }),
            section.SeasonId ? h('a', { class: 'el-button', href: web.detailsHref(section.SeasonId), title: 'Open section', 'aria-label': 'Open section', onclick: function (e) { e.stopPropagation(); } }, icon('chevron_right')) : null);
        return h('details', { class: 'el-section', open: options.open }, summary, lectures, resources);
    }

    function coursePanel(outline, refresh) {
        const next = nextLecture(outline);
        const openSection = next ? next.section : outline.Sections[0];
        return [
            progressBar(outline.Progress, 'Course progress'),
            h('div', { class: 'el-block' },
                h('a', { class: 'el-continue', href: web.playerHref(outline.SeriesId) },
                    icon('play_arrow'),
                    h('span', { text: next ? (outline.Progress.Completed ? 'Continue course: ' : 'Start course: ') + next.lecture.Title : 'Open course' }))),
            fileList('Course resources', outline.Resources, refresh),
            h('div', { class: 'el-block' },
                h('h3', { class: 'el-heading', text: 'Course content' }),
                ...outline.Sections.map(function (section) {
                    return sectionBlock(section, { refresh: refresh, open: section === openSection });
                }))
        ];
    }

    function sectionPanel(outline, section, refresh) {
        return [
            progressBar(section.Progress, 'Section progress'),
            fileList('Section resources', section.Resources, refresh),
            h('div', { class: 'el-block' },
                h('h3', { class: 'el-heading', text: 'Section content' }),
                h('ol', { class: 'el-lectures' }, ...section.Lectures.map(function (lecture) {
                    return lectureRow(lecture, { refresh: refresh });
                }))),
            h('p', { class: 'el-meta el-block' }, h('a', { class: 'el-link', href: web.detailsHref(outline.SeriesId), text: outline.Title }), ' • ' + outline.Progress.Percent + '% complete')
        ];
    }

    function lecturePanel(outline, section, lecture, refresh) {
        const lectures = allLectures(outline);
        const index = lectures.findIndex(function (x) { return x.lecture === lecture; });
        const upNext = lectures.slice(index + 1, index + 4);
        return [
            h('div', { class: 'el-block' },
                h('a', { class: 'el-continue', href: web.playerHref(outline.SeriesId, lecture.Id) }, icon('play_arrow'), h('span', { text: 'Open in course player' }))),
            h('p', { class: 'el-meta el-block' },
                h('a', { class: 'el-link', href: web.detailsHref(outline.SeriesId), text: outline.Title }),
                ' › ' + section.Title + ' • lecture ' + (section.Lectures.indexOf(lecture) + 1) + ' of ' + section.Lectures.length +
                ' • course ' + outline.Progress.Percent + '% complete'),
            fileList('Lecture resources', lecture.Attachments, refresh),
            upNext.length ? h('div', { class: 'el-block' },
                h('h3', { class: 'el-heading', text: 'Up next' }),
                h('ol', { class: 'el-lectures' }, ...upNext.map(function (x) { return lectureRow(x.lecture, { refresh: refresh }); }))) : null
        ];
    }

    // ---- Page hook --------------------------------------------------------------------------------------

    let renderToken = 0;
    let headingObserver = null;

    function stopRelabel() {
        if (headingObserver) {
            headingObserver.disconnect();
            headingObserver = null;
        }
    }

    async function render(page, itemId) {
        const token = ++renderToken;
        let outline;
        try {
            outline = await getOutline(itemId);
        } catch (err) {
            console.warn('[E-Learning] ' + err.message);
            return;
        }
        if (token !== renderToken || !outline || web.itemIdFromUrl() !== itemId) {
            return;
        }
        const refresh = function () {
            notCourses.delete(itemId);
            render(page, itemId);
        };

        activeSeriesId = outline.SeriesId;
        let content = null;
        let kind = 'course';
        if (outline.SeriesId === itemId) {
            content = coursePanel(outline, refresh);
        } else {
            for (const section of outline.Sections) {
                if (section.SeasonId === itemId) {
                    kind = 'section';
                    content = sectionPanel(outline, section, refresh);
                    break;
                }
                const lecture = section.Lectures.find(function (l) { return l.ItemId === itemId; });
                if (lecture) {
                    kind = 'lecture';
                    content = lecturePanel(outline, section, lecture, refresh);
                    break;
                }
            }
        }
        if (!content) {
            return;
        }

        const panel = h('section', { class: 'el-panel el-panel-' + kind + ' verticalSection detailVerticalSection', 'data-item-id': itemId }, ...content);
        page.querySelectorAll('.el-panel').forEach(function (old) { old.remove(); });
        web.mount(page, panel);
        stopRelabel();
        headingObserver = web.relabel(page, kind);
    }

    document.addEventListener('viewshow', function (e) {
        try {
            stopRelabel();
            const page = web.detailPage(e.target);
            const itemId = page && web.itemIdFromUrl();
            if (page) {
                page.querySelectorAll('.el-panel').forEach(function (old) {
                    if (old.getAttribute('data-item-id') !== itemId) {
                        old.remove();
                    }
                });
            }
            if (page && itemId && web.api()) {
                render(page, itemId);
            }
        } catch (err) {
            console.warn('[E-Learning] ' + err.message);
        }
    });

    // ---- Styles -----------------------------------------------------------------------------------------

    const css = `
.el-panel { margin-bottom: 1.5em; max-width: 60em; }
.el-block { margin: 1em 0; }
.el-heading { margin: 0 0 .5em; font-size: 1.15em; font-weight: 500; }
.el-icon { font-size: 1.3em; vertical-align: middle; }
.el-meta { opacity: .7; font-size: .9em; white-space: nowrap; display: inline-flex; align-items: center; gap: .2em; }
.el-meta .el-icon { font-size: 1.1em; }
.el-link { color: inherit; text-decoration: underline; }
.el-button { display: inline-flex; align-items: center; justify-content: center; width: 2.2em; height: 2.2em; border: 0; border-radius: 50%;
  background: transparent; color: inherit; cursor: pointer; text-decoration: none; padding: 0; flex-shrink: 0; }
.el-button:hover, .el-button:focus-visible { background: rgba(128,128,128,.25); }
.el-done, .el-done .el-icon { color: #00a4dc; }
.el-progress { max-width: 40em; }
.el-progress-text { display: flex; justify-content: space-between; gap: 1em; margin-bottom: .35em; flex-wrap: wrap; }
.el-progress-track, .el-resume { height: .4em; border-radius: .2em; background: rgba(128,128,128,.3); overflow: hidden; }
.el-resume { height: .2em; margin-top: .25em; max-width: 20em; }
.el-progress-fill { height: 100%; background: #00a4dc; }
.el-continue { text-decoration: none; display: inline-flex; align-items: center; gap: .5em; padding: .6em 1.2em; border: 0; border-radius: 2em; cursor: pointer;
  background: #00a4dc; color: #fff; font: inherit; max-width: 100%; text-align: left; }
.el-continue span:last-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.el-section { border-bottom: 1px solid rgba(128,128,128,.25); }
.el-section-summary { display: flex; align-items: center; gap: .75em; padding: .6em 0; cursor: pointer; list-style: none; }
.el-section-summary::-webkit-details-marker { display: none; }
.el-section-summary::before { content: 'expand_more'; font-family: 'Material Icons'; font-size: 1.4em; transform: rotate(-90deg); transition: transform .15s; }
.el-section[open] > .el-section-summary::before { transform: none; }
.el-section-title { flex: 1; font-weight: 500; }
.el-lectures, .el-files { list-style: none; margin: 0; padding: 0; }
.el-section .el-lectures { padding: 0 0 .5em 1.9em; }
.el-lecture-row, .el-file { display: flex; align-items: center; gap: .6em; padding: .3em 0; min-height: 2.2em; }
.el-lecture-main { flex: 1; min-width: 0; }
.el-lecture-title, .el-file-name { display: block; width: 100%; background: none; border: 0; padding: 0; color: inherit; font: inherit;
  text-align: left; cursor: pointer; text-decoration: none; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.el-lecture-title:hover, .el-file-name:hover { text-decoration: underline; }
.el-lecture-title:disabled { cursor: default; text-decoration: none; opacity: .8; }
.el-file-name { flex: 1; min-width: 0; width: auto; }
.el-current > .el-lecture-row { font-weight: 600; }
.el-completed .el-lecture-title { opacity: .75; }
.el-kind { opacity: .75; }
.el-status { width: 1.7em; height: 1.7em; }
span.el-status { display: inline-flex; align-items: center; justify-content: center; opacity: .8; }
.el-actions { display: inline-flex; }
.el-attachments { padding-left: 4.2em; font-size: .92em; }
.el-attachments[hidden] { display: none; }
.el-attach-toggle { background: none; border: 0; color: inherit; font: inherit; cursor: pointer; padding: .2em .4em; border-radius: 1em; }
.el-attach-toggle:hover, .el-attach-toggle[aria-expanded="true"] { background: rgba(128,128,128,.25); opacity: 1; }
.el-viewer { position: fixed; inset: 0; z-index: 2000; display: flex; flex-direction: column; background: #101010; color: #eee; }
.el-viewer-header { display: flex; align-items: center; gap: .5em; padding: .4em .8em; background: #1c1c1c; }
.el-viewer-title { flex: 1; margin: 0; font-size: 1.1em; font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.el-viewer-body { flex: 1; min-height: 0; display: flex; }
.el-viewer-message { margin: auto; text-align: center; }
.el-frame { flex: 1; border: 0; width: 100%; height: 100%; }
.el-frame-document { background: #fff; }
.el-image { flex: 1; display: flex; overflow: auto; }
.el-image img { margin: auto; max-width: 100%; max-height: 100%; object-fit: contain; }
@media (max-width: 40em) { .el-attachments { padding-left: 1em; } .el-section .el-lectures { padding-left: .5em; } }
`;
    document.head.append(h('style', { id: 'elearning-style', text: css }));
})();
