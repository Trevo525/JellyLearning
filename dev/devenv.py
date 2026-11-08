"""Shared by the dev scripts: loads dev/.env (written by dev/setup.py) into the environment, so
the scripts run without `set -a; . dev/.env`, and finds the dev state folder (DEV_STATE, as in
dev/compose.yaml; default dev/jellyfin)."""
import os
from pathlib import Path

DEV = Path(__file__).resolve().parent
env_file = DEV / ".env"
if env_file.exists():
    for line in env_file.read_text().splitlines():
        key, sep, value = line.partition("=")
        if sep and not key.lstrip().startswith("#"):
            os.environ.setdefault(key.strip(), value.strip())

os.environ.setdefault("JELLYFIN_URL", f"http://localhost:{os.environ.get('JELLYFIN_PORT', '8096')}")
STATE = DEV / os.environ.get("DEV_STATE", "jellyfin")  # relative to dev/, like in compose
ELEARNING_DATA = STATE / "config/data/data/elearning"
# Host folder of /data/elearning (real courses), if one is mounted.
REAL_COURSES = DEV / os.environ["ELEARNING_SOURCE"] if os.environ.get("ELEARNING_SOURCE") else STATE / "no-real-courses"
