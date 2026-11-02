#!/usr/bin/env bash
# Print how Jellyfin mapped each course (Series -> Season -> Episode), in Jellyfin's own
# display order, straight from the dev instance's database (read-only).
#   dev/library-report.sh [path-substring]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
db="$root/dev/jellyfin/config/data/data/jellyfin.db"
filter="${1:-}"

sqlite3 -readonly -separator $'\t' "$db" <<SQL
.mode list
WITH s AS (
  SELECT Id, Name, Path, ProductionYear, Studios FROM BaseItems
  WHERE Type = 'MediaBrowser.Controller.Entities.TV.Series' AND Path LIKE '%' || '$filter' || '%'
)
SELECT line FROM (
  SELECT s.Path AS k1, -1 AS k2, -1 AS k3, '' AS k4,
         '### ' || s.Name || '   [year=' || ifnull(s.ProductionYear,'-') || ' studio=' || ifnull(s.Studios,'-') || ']'
         || char(10) || '    path: ' || replace(s.Path, '/data/elearning/', '') AS line
  FROM s
  UNION ALL
  SELECT s.Path, ifnull(se.IndexNumber, 9999), -1, '',
         '  S' || ifnull(se.IndexNumber,'?') || '  "' || se.Name || '"'
         || CASE WHEN se.Path IS NULL THEN '  (virtual)' ELSE '  <- ' || replace(se.Path, s.Path || '/', '') END
  FROM s JOIN BaseItems se ON se.SeriesId = s.Id AND se.Type = 'MediaBrowser.Controller.Entities.TV.Season'
  UNION ALL
  SELECT s.Path, ifnull(e.ParentIndexNumber, 9999), ifnull(e.IndexNumber, 9999), ifnull(e.SortName, e.Name),
         '      E' || ifnull(e.IndexNumber,'?') || CASE WHEN e.ParentIndexNumber IS NULL THEN ' (S?)' ELSE '' END
         || '  "' || e.Name || '"  <- ' || replace(e.Path, s.Path || '/', '')
  FROM s JOIN BaseItems e ON e.SeriesId = s.Id AND e.Type = 'MediaBrowser.Controller.Entities.TV.Episode'
)
ORDER BY k1, k2, k3, k4;
SELECT char(10) || 'Items not under any Series in this library:';
SELECT '  ' || replace(Type, 'MediaBrowser.Controller.Entities.', '') || '  ' || replace(Path, '/data/elearning/', '')
FROM BaseItems WHERE Path LIKE '/data/elearning/%' AND SeriesId IS NULL
  AND Type NOT IN ('MediaBrowser.Controller.Entities.TV.Series', 'MediaBrowser.Controller.Entities.CollectionFolder');
SQL
