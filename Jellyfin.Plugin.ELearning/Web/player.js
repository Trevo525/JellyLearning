// E-Learning course player: a Udemy-style course page (video or document on the left, course content
// on the right) served by the plugin at <server>/ELearning/Web/player.html#<seriesId>[/<lectureId>].
//
// It signs in with the session jellyfin-web stored in this browser (same origin), plays videos with
// Jellyfin's own streaming (direct play, or HLS via Jellyfin transcoding/remuxing for mkv) and
// reports playback like jellyfin-web does, so resume positions and the course completion rule apply.
(function () {
    'use strict';

    const TICKS = 1e7;
    const base = location.pathname.slice(0, location.pathname.indexOf('/ELearning/Web/'));
    const $ = function (id) { return document.getElementById(id); };

    // ---- Icons (Material Icons paths, Apache-2.0) ---------------------------------------------------

    const paths = {
        back: 'M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z',
        close: 'M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z',
        expand: 'M16.59 8.59L12 13.17 7.41 8.59 6 10l6 6 6-6z',
        video: 'M10 16.5l6-4.5-6-4.5v9zM12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z',
        article: 'M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z',
        quiz: 'M11 18h2v-2h-2v2zm1-16C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm0-14c-2.21 0-4 1.79-4 4h2c0-1.1.9-2 2-2s2 .9 2 2c0 2-3 1.75-3 5h2c0-2.25 3-2.5 3-5 0-2.21-1.79-4-4-4z',
        link: 'M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z',
        download: 'M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z',
        folder: 'M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z',
        open: 'M19 19H5V5h7V3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2v-7h-2v7zM14 3v2h3.59l-9.83 9.83 1.41 1.41L19 6.41V10h2V3h-7z',
        next: 'M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z',
        previous: 'M6 6h2v12H6zm3.5 6l8.5 6V6z',
        image: 'M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z',
        check: 'M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z'
    };

    function icon(name) {
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('class', 'icon');
        svg.setAttribute('aria-hidden', 'true');
        const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        path.setAttribute('d', paths[name]);
        svg.append(path);
        return svg;
    }

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
            if (child !== null && child !== undefined && child !== false) {
                el.append(child);
            }
        }
        return el;
    }

    // ---- Session ------------------------------------------------------------------------------------

    let token = null;
    let userId = null;
    let serverId = null;

    async function signIn() {
        const info = await fetch(base + '/System/Info/Public').then(function (r) { return r.json(); });
        serverId = info.Id;
        let credentials = null;
        try {
            credentials = JSON.parse(localStorage.getItem('jellyfin_credentials') || 'null');
        } catch (e) {
            credentials = null;
        }
        const server = ((credentials && credentials.Servers) || []).find(function (s) { return s.Id === serverId && s.AccessToken && s.UserId; });
        if (!server) {
            return false;
        }
        token = server.AccessToken;
        userId = server.UserId;
        return true;
    }

    // Only the token: Jellyfin then takes client and device from the token's own (jellyfin-web) device.
    function headers(json) {
        const result = { Authorization: 'MediaBrowser Token="' + token + '"' };
        if (json) {
            result['Content-Type'] = 'application/json';
        }
        return result;
    }

    function api(path, options) {
        const opts = Object.assign({}, options || {});
        opts.headers = headers(opts.body !== undefined);
        if (opts.body !== undefined && typeof opts.body !== 'string') {
            opts.body = JSON.stringify(opts.body);
        }
        return fetch(base + '/' + path, opts);
    }

    async function apiJson(path, options) {
        const response = await api(path, options);
        if (!response.ok) {
            throw new Error(path.split('?')[0] + ': HTTP ' + response.status);
        }
        return response.status === 204 ? null : response.json();
    }

    // Downloads and media elements can't send headers; jellyfin-web uses api_key the same way.
    function withKey(url) {
        return url + (url.indexOf('?') < 0 ? '?' : '&') + 'api_key=' + encodeURIComponent(token);
    }

    function downloadUrl(fileId) {
        return withKey(base + '/ELearning/Files/' + encodeURIComponent(fileId) + '?download=true');
    }

    function webUrl(itemId) {
        return base + '/web/#/details?id=' + encodeURIComponent(itemId) + '&serverId=' + encodeURIComponent(serverId);
    }

    // ---- Outline helpers --------------------------------------------------------------------------------

    let outline = null;
    let current = null; // { lecture, section, index }
    const openSections = new Set(); // section indices

    function entries() {
        const list = [];
        outline.Sections.forEach(function (section, s) {
            section.Lectures.forEach(function (lecture) {
                list.push({ lecture: lecture, section: section, sectionIndex: s, index: list.length });
            });
        });
        return list;
    }

    function find(lectureId) {
        return entries().find(function (e) { return e.lecture.Id === lectureId; }) || null;
    }

    function resumeEntry() {
        const all = entries();
        return all.find(function (e) { return !e.lecture.Completed && e.lecture.PositionTicks > 0; })
            || all.find(function (e) { return !e.lecture.Completed; })
            || all[0] || null;
    }

    function isQuiz(lecture) {
        return /\bquiz\b/i.test(lecture.Title || lecture.Name || '');
    }

    function kindIcon(item) {
        if (item.ExternalUrl) {
            return 'link';
        }
        if (item.Kind === 'Video') {
            return 'video';
        }
        if (item.Kind === 'Image') {
            return 'image';
        }
        if (item.Kind === 'Html' && isQuiz(item)) {
            return 'quiz';
        }
        return item.Kind === 'Archive' || item.Kind === 'Document' || item.Kind === 'Other' ? 'folder' : 'article';
    }

    function kindLabel(lecture) {
        if (lecture.Kind === 'Video') {
            return formatMinutes(lecture.RunTimeTicks) || 'Video';
        }
        if (lecture.ExternalUrl) {
            return 'Link';
        }
        if (isQuiz(lecture)) {
            return 'Quiz';
        }
        return lecture.Kind === 'Pdf' ? 'PDF' : 'Reading';
    }

    function formatMinutes(ticks) {
        if (!ticks) {
            return '';
        }
        const minutes = Math.max(1, Math.round(ticks / TICKS / 60));
        const hours = Math.floor(minutes / 60);
        return hours ? hours + 'hr ' + (minutes % 60) + 'min' : minutes + 'min';
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

    function videoTicks(lectures) {
        return lectures.reduce(function (sum, l) { return sum + (l.Kind === 'Video' ? (l.RunTimeTicks || 0) : 0); }, 0);
    }

    async function reloadOutline() {
        outline = await apiJson('ELearning/Courses/' + encodeURIComponent(outline.SeriesId) + '/Outline');
        if (current) {
            current = find(current.lecture.Id) || current;
        }
        renderHeader();
        renderSidebar();
        renderLectureBar();
        renderResources();
    }

    // ---- Completion --------------------------------------------------------------------------------------

    async function setCompleted(lecture, completed) {
        if (lecture.Kind === 'Video') {
            if (!lecture.ItemId) {
                return;
            }
            await api('UserPlayedItems/' + encodeURIComponent(lecture.ItemId) + '?userId=' + encodeURIComponent(userId), { method: completed ? 'POST' : 'DELETE' });
        } else {
            await api('ELearning/Lectures/' + encodeURIComponent(lecture.Id) + '/Complete', { method: completed ? 'POST' : 'DELETE' });
        }
        lecture.Completed = completed;
        await reloadOutline();
    }

    // ---- Video playback ------------------------------------------------------------------------------

    let playback = null; // { itemId, mediaSourceId, playSessionId, playMethod, video, hls, timer, stopped }

    function deviceProfile(video) {
        const can = function (type) { return video.canPlayType(type).replace('no', '') !== ''; };
        const videoCodecs = ['h264'];
        if (can('video/mp4; codecs="hvc1.1.6.L93.B0"')) {
            videoCodecs.push('hevc');
        }
        if (can('video/mp4; codecs="av01.0.05M.08"')) {
            videoCodecs.push('av1');
        }
        const directPlay = [{ Container: 'mp4,m4v', Type: 'Video', VideoCodec: videoCodecs.join(','), AudioCodec: 'aac,mp3,opus,flac' }];
        if (can('video/webm')) {
            directPlay.push({ Container: 'webm', Type: 'Video', VideoCodec: 'vp8,vp9,av1', AudioCodec: 'vorbis,opus' });
        }
        return {
            MaxStreamingBitrate: 120000000,
            MaxStaticBitrate: 100000000,
            DirectPlayProfiles: directPlay,
            TranscodingProfiles: [{
                Container: 'ts', Type: 'Video', VideoCodec: 'h264', AudioCodec: 'aac,mp3', Context: 'Streaming',
                Protocol: 'hls', MaxAudioChannels: '2', MinSegments: 1, BreakOnNonKeyFrames: true
            }],
            ContainerProfiles: [],
            CodecProfiles: [],
            SubtitleProfiles: [{ Format: 'vtt', Method: 'External' }]
        };
    }

    function report(endpoint, extra, keepalive) {
        if (!playback) {
            return Promise.resolve();
        }
        const video = playback.video;
        const body = Object.assign({
            ItemId: playback.itemId,
            MediaSourceId: playback.mediaSourceId,
            PlaySessionId: playback.playSessionId,
            PlayMethod: playback.playMethod,
            CanSeek: true,
            IsPaused: video.paused,
            IsMuted: video.muted,
            VolumeLevel: Math.round(video.volume * 100),
            PositionTicks: Math.round((video.currentTime || 0) * TICKS)
        }, extra || {});
        return api('Sessions/Playing' + endpoint, { method: 'POST', body: body, keepalive: !!keepalive }).catch(function () { });
    }

    // Returns the stop report's promise, or null when nothing was playing.
    function stopPlayback(keepalive) {
        if (!playback || playback.stopped) {
            return null;
        }
        playback.stopped = true;
        clearInterval(playback.timer);
        const done = report('/Stopped', null, keepalive);
        if (playback.hls) {
            playback.hls.destroy();
        }
        playback.video.removeAttribute('src');
        playback.video.load();
        playback = null;
        return done;
    }

    async function playVideo(entry, stage) {
        const lecture = entry.lecture;
        if (!lecture.ItemId) {
            stage.append(message('Not in the library yet', 'This video hasn’t been added by a library scan yet.'));
            return;
        }
        const video = h('video', { controls: true, playsinline: true, preload: 'auto' });
        stage.append(video);

        const [info, userData] = await Promise.all([
            apiJson('Items/' + encodeURIComponent(lecture.ItemId) + '/PlaybackInfo', {
                method: 'POST',
                body: { UserId: userId, DeviceProfile: deviceProfile(video), AutoOpenLiveStream: false, EnableDirectPlay: true, EnableDirectStream: true, EnableTranscoding: true, AllowVideoStreamCopy: true, AllowAudioStreamCopy: true }
            }),
            apiJson('UserItems/' + encodeURIComponent(lecture.ItemId) + '/UserData?userId=' + encodeURIComponent(userId)).catch(function () { return null; })
        ]);
        if (current !== entry) {
            return;
        }
        const source = info.MediaSources && info.MediaSources[0];
        if (!source) {
            throw new Error('no playable source');
        }

        let url;
        let playMethod;
        if (source.SupportsDirectPlay) {
            url = withKey(base + '/Videos/' + encodeURIComponent(lecture.ItemId) + '/stream?static=true&mediaSourceId=' + encodeURIComponent(source.Id));
            playMethod = 'DirectPlay';
        } else if (source.TranscodingUrl) {
            url = base + source.TranscodingUrl;
            if (!/[?&](api_key|ApiKey)=/i.test(url)) {
                url = withKey(url);
            }
            playMethod = source.SupportsDirectStream ? 'DirectStream' : 'Transcode';
        } else {
            throw new Error('the browser can’t play this file and Jellyfin offered no transcode');
        }

        // Sidecar subtitles (Jellyfin converts them to WebVTT).
        (source.MediaStreams || []).filter(function (s) { return s.Type === 'Subtitle' && s.DeliveryUrl; }).forEach(function (s, i) {
            video.append(h('track', { kind: 'subtitles', src: base + s.DeliveryUrl, srclang: s.Language || 'und', label: s.DisplayTitle || s.Language || 'Subtitles ' + (i + 1) }));
        });

        const start = userData && !userData.Played && userData.PlaybackPositionTicks ? userData.PlaybackPositionTicks / TICKS : 0;
        playback = { itemId: lecture.ItemId, mediaSourceId: source.Id, playSessionId: info.PlaySessionId, playMethod: playMethod, video: video, hls: null, timer: null, stopped: false };
        const session = playback;

        const isHls = /\.m3u8(\?|$)/i.test(url.split('?')[0]) || /m3u8/i.test(source.TranscodingSubProtocol || '') || source.TranscodingSubProtocol === 'hls';
        if (isHls && !video.canPlayType('application/vnd.apple.mpegurl') && window.Hls && window.Hls.isSupported()) {
            session.hls = new window.Hls({ startPosition: start });
            session.hls.loadSource(url);
            session.hls.attachMedia(video);
        } else {
            video.src = url;
            if (start) {
                video.addEventListener('loadedmetadata', function () { video.currentTime = start; }, { once: true });
            }
        }

        let started = false;
        video.addEventListener('playing', function () {
            if (!started) {
                started = true;
                report('', { PositionTicks: Math.round(video.currentTime * TICKS) });
                session.timer = setInterval(function () {
                    if (!video.paused && !session.stopped) {
                        report('/Progress', { EventName: 'timeupdate' });
                    }
                }, 10000);
            } else {
                report('/Progress', { EventName: 'unpause' });
            }
        });
        video.addEventListener('pause', function () {
            if (started && !video.ended && !session.stopped) {
                report('/Progress', { EventName: 'pause' });
            }
        });
        video.addEventListener('seeked', function () {
            if (started && !session.stopped) {
                report('/Progress', { EventName: 'timeupdate' });
            }
        });
        video.addEventListener('ended', function () {
            if (session.stopped) {
                return;
            }
            stopPlayback().then(function () { setTimeout(reloadOutline, 1500); });
            showUpNext(entry);
        });

        video.play().catch(function () { /* Autoplay blocked: the controls are there. */ });
    }

    // ---- Documents ---------------------------------------------------------------------------------------

    async function showDocument(entry, stage) {
        const lecture = entry.lecture;
        if (!lecture.Completed) {
            // Opening a reading or quiz completes it, like on the course site (the checkbox undoes it).
            setCompleted(lecture, true).catch(function () { });
        }
        if (lecture.ExternalUrl) {
            stage.append(message(lecture.Title, 'This lecture is an external page.',
                h('a', { class: 'button primary', href: lecture.ExternalUrl, target: '_blank', rel: 'noopener noreferrer' }, icon('open'), 'Open ' + new URL(lecture.ExternalUrl).hostname)));
            return;
        }
        stage.classList.add('document');
        await renderFileInto(stage, { Id: lecture.Id, Name: lecture.Title, Kind: lecture.Kind });
    }

    const objectUrls = [];

    async function renderFileInto(stage, file) {
        const id = encodeURIComponent(file.Id);
        if (file.Kind === 'Html' || file.Kind === 'Markdown' || file.Kind === 'Text') {
            // Fetched with the token in a header and shown in a sandbox without same-origin access,
            // so scripts in exported quizzes can run but can't reach the Jellyfin session.
            const response = await api('ELearning/Files/' + id + '/Render');
            if (!response.ok) {
                throw new Error('HTTP ' + response.status);
            }
            let html = await response.text();
            if (!/<base\b/i.test(html)) {
                html = /<head[^>]*>/i.test(html) ? html.replace(/<head[^>]*>/i, '$&<base target="_blank">') : '<base target="_blank">' + html;
            }
            const sandbox = (file.Kind === 'Html' ? 'allow-scripts ' : '') + 'allow-popups allow-popups-to-escape-sandbox';
            stage.replaceChildren(h('iframe', { sandbox: sandbox, srcdoc: html, title: file.Name, referrerpolicy: 'no-referrer' }));
            return;
        }
        const response = await api('ELearning/Files/' + id);
        if (!response.ok) {
            throw new Error('HTTP ' + response.status);
        }
        const blob = await response.blob();
        const url = URL.createObjectURL(file.Kind === 'Pdf' ? new Blob([blob], { type: 'application/pdf' }) : blob);
        objectUrls.push(url);
        stage.replaceChildren(file.Kind === 'Image'
            ? h('div', { class: 'stage-image' }, h('img', { src: url, alt: file.Name }))
            : h('iframe', { src: url, title: file.Name }));
    }

    function message(title, text, action) {
        return h('div', { class: 'stage-message' }, h('h2', { text: title }), h('p', { text: text }), action || null);
    }

    // ---- Navigation ---------------------------------------------------------------------------------------

    let upNextTimer = null;

    function showUpNext(entry) {
        const next = entries()[entry.index + 1];
        const stage = $('stage');
        if (!next) {
            stage.append(h('div', { class: 'up-next' }, h('div', { class: 'up-next-card' },
                h('div', { class: 'title', text: 'You’ve reached the end of the course' }),
                h('div', { class: 'actions' }, h('a', { class: 'button', href: webUrl(outline.SeriesId) }, 'Back to the course page')))));
            return;
        }
        let seconds = 5;
        const label = h('div', { class: 'label', text: 'Up next in ' + seconds + 's' });
        const cancel = function () {
            clearInterval(upNextTimer);
            upNextTimer = null;
            overlay.remove();
        };
        const overlay = h('div', { class: 'up-next' }, h('div', { class: 'up-next-card' },
            label,
            h('div', { class: 'title', text: next.lecture.Title }),
            h('div', { class: 'actions' },
                h('button', { type: 'button', class: 'button', onclick: cancel }, 'Cancel'),
                h('button', { type: 'button', class: 'button primary', onclick: function () { cancel(); go(next); } }, icon('next'), 'Play now'))));
        stage.append(overlay);
        upNextTimer = setInterval(function () {
            seconds--;
            label.textContent = 'Up next in ' + seconds + 's';
            if (seconds <= 0) {
                cancel();
                go(next);
            }
        }, 1000);
    }

    async function go(entry) {
        if (!entry) {
            return;
        }
        if (upNextTimer) {
            clearInterval(upNextTimer);
            upNextTimer = null;
        }
        const stopped = stopPlayback();
        objectUrls.splice(0).forEach(URL.revokeObjectURL);

        current = entry;
        openSections.add(entry.sectionIndex);
        history.replaceState(null, '', '#' + outline.SeriesId + '/' + entry.lecture.Id);
        document.title = entry.lecture.Title + ' – ' + outline.Title;

        const stage = $('stage');
        stage.className = 'stage';
        stage.replaceChildren();
        renderSidebar();
        renderLectureBar();
        renderResources();
        scrollCurrentIntoView();

        try {
            if (entry.lecture.Kind === 'Video') {
                await playVideo(entry, stage);
            } else {
                await showDocument(entry, stage);
            }
        } catch (err) {
            stage.replaceChildren(message('This lecture can’t be shown here', err.message,
                h('a', { class: 'button', href: downloadUrl(entry.lecture.Id) }, icon('download'), 'Download')));
        }
        if (stopped) {
            // The completion rule is applied when Jellyfin raises the (asynchronous) stop event.
            stopped.then(function () { setTimeout(reloadOutline, 1500); });
        }
    }

    function scrollCurrentIntoView() {
        const el = document.querySelector('.lecture.current');
        if (el && el.scrollIntoView) {
            el.scrollIntoView({ block: 'nearest' });
        }
    }

    // ---- Rendering ------------------------------------------------------------------------------------------

    function progressRing(percent) {
        const ns = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(ns, 'svg');
        svg.setAttribute('viewBox', '0 0 36 36');
        svg.setAttribute('aria-hidden', 'true');
        const circle = function (color, dash) {
            const c = document.createElementNS(ns, 'circle');
            c.setAttribute('cx', '18');
            c.setAttribute('cy', '18');
            c.setAttribute('r', '15.9');
            c.setAttribute('fill', 'none');
            c.setAttribute('stroke', color);
            c.setAttribute('stroke-width', '3');
            if (dash !== undefined) {
                c.setAttribute('stroke-dasharray', dash + ' 100');
                c.setAttribute('transform', 'rotate(-90 18 18)');
            }
            return c;
        };
        svg.append(circle('#2e2e2e'), circle('#00a4dc', percent));
        return svg;
    }

    function renderHeader() {
        $('course-title').textContent = outline.Title;
        const back = $('back');
        back.href = webUrl(outline.SeriesId);
        back.replaceChildren(icon('back'));
        const progress = $('course-progress');
        progress.hidden = false;
        progress.replaceChildren(progressRing(outline.Progress.Percent),
            h('span', { text: outline.Progress.Completed + ' of ' + outline.Progress.Total + ' complete' }));
    }

    function fileRow(file) {
        const open = function () {
            if (file.ExternalUrl) {
                window.open(file.ExternalUrl, '_blank', 'noopener,noreferrer');
            } else if (['Pdf', 'Html', 'Markdown', 'Text', 'Image'].indexOf(file.Kind) >= 0) {
                openFileInStage(file);
            } else {
                location.href = downloadUrl(file.Id);
            }
        };
        return h('li', { class: 'file' },
            icon(kindIcon(file)),
            h('button', { type: 'button', class: 'file-name', onclick: open, text: file.Name, title: file.Name }),
            h('span', { class: 'file-meta', text: file.ExternalUrl ? new URL(file.ExternalUrl).hostname : formatSize(file.Size) }),
            file.ExternalUrl
                ? h('a', { class: 'icon-button', href: file.ExternalUrl, target: '_blank', rel: 'noopener noreferrer', title: 'Open link', 'aria-label': 'Open link' }, icon('open'))
                : h('a', { class: 'icon-button', href: downloadUrl(file.Id), title: 'Download', 'aria-label': 'Download ' + file.Name }, icon('download')));
    }

    // A resource opened from a lecture replaces the stage; the lecture bar offers the way back.
    async function openFileInStage(file) {
        const stopped = stopPlayback();
        const stage = $('stage');
        stage.className = 'stage document';
        stage.replaceChildren(message('Loading…', file.Name));
        renderLectureBar(file);
        try {
            await renderFileInto(stage, file);
        } catch (err) {
            stage.replaceChildren(message('This file can’t be shown here', err.message, h('a', { class: 'button', href: downloadUrl(file.Id) }, icon('download'), 'Download')));
        }
        if (stopped) {
            stopped.then(function () { setTimeout(reloadOutline, 1500); });
        }
    }

    function renderLectureBar(file) {
        const bar = $('lecture-bar');
        if (!current) {
            bar.hidden = true;
            return;
        }
        bar.hidden = false;
        const all = entries();
        const previous = all[current.index - 1];
        const next = all[current.index + 1];
        const lecture = current.lecture;
        bar.replaceChildren(
            h('button', { type: 'button', class: 'icon-button', title: 'Previous lecture', 'aria-label': 'Previous lecture', disabled: !previous, onclick: function () { go(previous); } }, icon('previous')),
            h('div', { class: 'lecture-bar-title', text: file ? file.Name + ' – resource of ' + lecture.Title : (current.index + 1) + '. ' + lecture.Title }),
            file ? h('button', { type: 'button', class: 'button', onclick: function () { go(current); } }, icon('back'), 'Back to lecture') : null,
            file ? h('a', { class: 'button', href: downloadUrl(file.Id) }, icon('download'), 'Download') : null,
            !file && lecture.Kind !== 'Video' ? h('a', { class: 'button', href: downloadUrl(lecture.Id) }, icon('download'), 'Download') : null,
            h('button', { type: 'button', class: 'button' + (next ? ' primary' : ''), disabled: !next, onclick: function () { go(next); } }, 'Next', icon('next')));
    }

    function renderSidebar() {
        const container = $('sections');
        const scroll = container.scrollTop;
        let number = 0;
        container.replaceChildren(...outline.Sections.map(function (section, s) {
            const lectures = section.Lectures;
            const open = openSections.has(s);
            const sectionEl = h('div', { class: 'section' + (open ? ' open' : '') });
            const head = h('button', { type: 'button', class: 'section-head', 'aria-expanded': String(open), onclick: function () {
                const isOpen = sectionEl.classList.toggle('open');
                head.setAttribute('aria-expanded', String(isOpen));
                if (isOpen) {
                    openSections.add(s);
                } else {
                    openSections.delete(s);
                }
            } },
            h('div', { class: 'section-head-text' },
                h('div', { class: 'section-title', text: 'Section ' + (s + 1) + ': ' + section.Title }),
                h('div', { class: 'section-meta', text: section.Progress.Completed + ' / ' + section.Progress.Total + (videoTicks(lectures) ? ' | ' + formatMinutes(videoTicks(lectures)) : '') })),
            icon('expand'));
            const list = h('ul', { class: 'section-lectures' }, ...lectures.map(function (lecture) {
                number++;
                return lectureItem(lecture, number);
            }));
            sectionEl.append(head, list);
            return sectionEl;
        }));
        container.scrollTop = scroll;
    }

    function lectureItem(lecture, number) {
        const isCurrent = current && current.lecture.Id === lecture.Id;
        const check = h('input', {
            type: 'checkbox', class: 'lecture-check', checked: lecture.Completed,
            title: lecture.Completed ? 'Mark as not completed' : 'Mark as completed',
            'aria-label': (lecture.Completed ? 'Mark as not completed: ' : 'Mark as completed: ') + lecture.Title,
            disabled: lecture.Kind === 'Video' && !lecture.ItemId,
            onclick: function (e) { e.stopPropagation(); },
            onchange: function () { setCompleted(lecture, check.checked).catch(function () { check.checked = !check.checked; }); }
        });

        let menu = null;
        let toggle = null;
        if (lecture.Attachments.length) {
            menu = h('ul', { class: 'resources-menu', hidden: true }, ...lecture.Attachments.map(fileRow));
            menu.addEventListener('click', function (e) { e.stopPropagation(); });
            toggle = h('button', { type: 'button', class: 'resources-toggle', 'aria-expanded': 'false', onclick: function (e) {
                e.stopPropagation();
                menu.hidden = !menu.hidden;
                toggle.setAttribute('aria-expanded', String(!menu.hidden));
            } }, icon('folder'), 'Resources', icon('expand'));
        }

        const resume = lecture.Kind === 'Video' && !lecture.Completed && lecture.PositionTicks > 0 && lecture.RunTimeTicks
            ? h('div', { class: 'lecture-resume' }, h('div', { style: 'width:' + Math.min(100, 100 * lecture.PositionTicks / lecture.RunTimeTicks).toFixed(1) + '%' }))
            : null;

        return h('li', {
            class: 'lecture' + (isCurrent ? ' current' : ''), tabindex: '0', 'aria-current': isCurrent ? 'true' : null,
            onclick: function () { const entry = find(lecture.Id); if (entry) { go(entry); } },
            onkeydown: function (e) { if (e.key === 'Enter' && e.target === this) { this.click(); } }
        },
        check,
        h('div', { class: 'lecture-body' },
            h('div', { class: 'lecture-title', text: number + '. ' + lecture.Title }),
            h('div', { class: 'lecture-meta' }, icon(kindIcon(lecture)), h('span', { text: kindLabel(lecture) }), toggle),
            resume,
            menu));
    }

    function renderOverview() {
        const all = entries();
        const videos = all.filter(function (e) { return e.lecture.Kind === 'Video'; });
        const documents = all.length - videos.length;
        const quizzes = all.filter(function (e) { return e.lecture.Kind !== 'Video' && isQuiz(e.lecture); }).length;
        const panel = $('tab-overview');
        const facts = [
            ['Sections', outline.Sections.length],
            ['Lectures', all.length],
            ['Video', formatMinutes(videoTicks(videos.map(function (e) { return e.lecture; }))) || '–'],
            ['Readings', documents - quizzes],
            ['Quizzes', quizzes],
            ['Resources', all.reduce(function (n, e) { return n + e.lecture.Attachments.length; }, 0) + outline.Resources.length + outline.Sections.reduce(function (n, s) { return n + s.Resources.length; }, 0)]
        ];
        panel.replaceChildren(
            h('h2', { text: outline.Title }),
            h('p', { class: 'muted', text: [outline.Studio, outline.Year].filter(Boolean).join(' • ') || null }),
            h('div', { class: 'facts' },
                h('div', { class: 'label', text: 'By the numbers' }),
                h('dl', null, ...facts.map(function (f) { return h('div', null, h('dt', { text: f[0] + ':' }), h('dd', { text: String(f[1]) })); }))),
            h('div', { class: 'facts' },
                h('div', { class: 'label', text: 'Your progress' }),
                h('div', { text: outline.Progress.Completed + ' of ' + outline.Progress.Total + ' lectures complete (' + outline.Progress.Percent + '%)' })),
            h('div', { class: 'facts', id: 'overview-description', hidden: true }));
        apiJson('Items/' + encodeURIComponent(outline.SeriesId) + '?userId=' + encodeURIComponent(userId)).then(function (item) {
            if (item && item.Overview) {
                const description = $('overview-description');
                description.hidden = false;
                description.replaceChildren(h('div', { class: 'label', text: 'Description' }), h('div', { class: 'overview-text', text: item.Overview }));
            }
        }).catch(function () { });
    }

    function renderResources() {
        const panel = $('tab-resources');
        const blocks = [];
        if (current && current.lecture.Attachments.length) {
            blocks.push(h('h3', { text: 'This lecture' }), h('ul', { class: 'files' }, ...current.lecture.Attachments.map(fileRow)));
        }
        if (current && current.section.Resources.length) {
            blocks.push(h('h3', { text: 'This section' }), h('ul', { class: 'files' }, ...current.section.Resources.map(fileRow)));
        }
        if (outline.Resources.length) {
            blocks.push(h('h3', { text: 'Course' }), h('ul', { class: 'files' }, ...outline.Resources.map(fileRow)));
        }
        panel.replaceChildren(...(blocks.length ? blocks : [h('p', { class: 'muted', text: 'No resources for this lecture.' })]));
    }

    function setupChrome() {
        document.querySelectorAll('.tab').forEach(function (tab) {
            tab.addEventListener('click', function () {
                document.querySelectorAll('.tab').forEach(function (t) {
                    const selected = t === tab;
                    t.setAttribute('aria-selected', String(selected));
                    $('tab-' + t.dataset.tab).hidden = !selected;
                });
            });
        });
        $('close-sidebar').append(icon('close'));
        $('close-sidebar').addEventListener('click', function () {
            $('layout').classList.add('sidebar-closed');
            $('open-sidebar').hidden = false;
        });
        $('open-sidebar').addEventListener('click', function () {
            $('layout').classList.remove('sidebar-closed');
            $('open-sidebar').hidden = true;
        });
        // Report the stop when the page goes away (tab closed, back to Jellyfin).
        window.addEventListener('pagehide', function () { stopPlayback(true); });
    }

    // ---- Start ---------------------------------------------------------------------------------------------------

    async function start() {
        setupChrome();
        const stage = $('stage');
        const [seriesId, lectureId] = location.hash.slice(1).split('/');
        if (!(await signIn())) {
            stage.append(message('Sign in to Jellyfin first', 'The course player uses your Jellyfin session from this browser.',
                h('a', { class: 'button primary', href: base + '/web/' }, 'Open Jellyfin')));
            return;
        }
        if (!seriesId) {
            stage.append(message('No course selected', 'Open a course from Jellyfin.', h('a', { class: 'button primary', href: base + '/web/' }, 'Open Jellyfin')));
            return;
        }
        try {
            outline = await apiJson('ELearning/Courses/' + encodeURIComponent(seriesId) + '/Outline');
        } catch (err) {
            stage.append(message('Course not found', 'It may have been removed, or you don’t have access to it.', h('a', { class: 'button primary', href: base + '/web/' }, 'Open Jellyfin')));
            return;
        }
        renderHeader();
        renderOverview();
        go((lectureId && find(lectureId)) || resumeEntry());
    }

    start().catch(function (err) {
        console.error('[E-Learning player]', err);
        $('stage').replaceChildren(message('Something went wrong', err.message));
    });
})();
