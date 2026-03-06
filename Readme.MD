# Lingarr: Subtitle Translation Made Easy (Fork)

> Hardened fork of [lingarr-translate/lingarr](https://github.com/lingarr-translate/lingarr) with security, correctness, reliability, and infrastructure improvements. All changes are intended to be contributed back upstream. No features were removed.

[![License](https://img.shields.io/badge/license-AGPL--3.0-green.svg)](LICENSE)

Lingarr automatically translates subtitle files to your desired target language using multiple translation backends:
LibreTranslate, Local AI (Ollama), DeepL, Anthropic, OpenAI, DeepSeek, Gemini, Google Translate, Bing, Yandex, and Azure.

https://github.com/user-attachments/assets/be753d77-ca53-4867-adea-363145613d2b

## Quick Start

```yaml
services:
  lingarr:
    image: ghcr.io/fisifla/lingarr:latest
    container_name: lingarr
    restart: unless-stopped
    environment:
      - ASPNETCORE_URLS=http://+:9876
      - DB_CONNECTION=mysql
      - DB_HOST=lingarr-db
      - DB_PORT=3306
      - DB_DATABASE=lingarr
      - DB_USERNAME=lingarr
      - DB_PASSWORD=your-password
    ports:
      - "9876:9876"
    volumes:
      - /path/to/media/movies:/movies
      - /path/to/media/tv:/tv
      - /path/to/config:/app/config
    depends_on:
      lingarr-db:
        condition: service_healthy

  lingarr-db:
    image: mariadb:latest
    container_name: lingarr-db
    restart: unless-stopped
    environment:
      - MYSQL_DATABASE=lingarr
      - MYSQL_USER=lingarr
      - MYSQL_PASSWORD=your-password
      - MYSQL_ROOT_PASSWORD=your-password
    volumes:
      - /path/to/db:/var/lib/mysql
    healthcheck:
      test: "mariadb $$MYSQL_DATABASE -u$$MYSQL_USER -p$$MYSQL_PASSWORD -e 'SELECT 1;'"
      interval: 10s
      timeout: 5s
      retries: 5
```

PostgreSQL and SQLite are also supported — set `DB_CONNECTION` accordingly.

## Docker Image

Multi-arch (amd64 + arm64), published on every push to `main`:

```
ghcr.io/fisifla/lingarr:latest
ghcr.io/fisifla/lingarr:main
```

| Tag | Description |
|-----|-------------|
| `latest` | Latest build from main |
| `main` | Same as latest (branch tag) |
| `v1.2.3` | Specific version (on tagged releases) |

## Environment Variables

| Variable | Description | Default |
|---|---|---|
| `ASPNETCORE_URLS` | Listen address | `http://+:9876` |
| `DB_CONNECTION` | `mysql`, `postgresql`, or `sqlite` | - |
| `DB_HOST` / `DB_PORT` | Database host and port | - |
| `DB_DATABASE` | Database name | - |
| `DB_USERNAME` / `DB_PASSWORD` | Database credentials (required for mysql/postgresql) | - |
| `DB_HANGFIRE_SQLITE_PATH` | Hangfire DB path when using sqlite | `/app/config/Hangfire.db` |
| `MAX_CONCURRENT_JOBS` | Parallel translation jobs | `1` |
| `ALLOWED_MEDIA_PATHS` | Allowed filesystem roots for browsing | `/media,/movies,/tv` |

Additional [settings](Settings.MD) can be set as environment variables to persist across reinstalls.

## LibreTranslate (Optional)

Only needed if using LibreTranslate as your translation backend:

```yaml
  libretranslate:
    image: libretranslate/libretranslate:latest
    container_name: libretranslate
    restart: unless-stopped
    environment:
      - LT_LOAD_ONLY=en,nl  # replace with your languages
    ports:
      - "5000:5000"
    volumes:
      - /path/to/config:/home/libretranslate/.local/share/argos-translate
    healthcheck:
      test: ["CMD-SHELL", "./venv/bin/python scripts/healthcheck.py"]
```

## API

RESTful API documentation available at [lingarr.com/docs/api/](https://lingarr.com/docs/api/).

## Changes from Upstream

### Security
- Added missing `UseAuthorization()` middleware — `[Authorize]` attributes were silently not enforced
- Hardened onboarding endpoints with concurrency lock to prevent multiple admin creation
- Migrated password hashing from BCrypt to PBKDF2 (transparent fallback for existing hashes)
- Fixed API key timing attack — now uses `CryptographicOperations.FixedTimeEquals`
- Added centralized `PathSecurity` utility; restricted directory browsing to `ALLOWED_MEDIA_PATHS`
- Fixed path traversal in `SubtitleController` and SSRF in `ImageController`
- HTML-escaped user content to prevent XSS in logs, subtitles, and template previews
- Removed hardcoded fallback database password

### Correctness
- Fixed SSA/ASS subtitle validation always failing (was hardcoded to SRT parser)
- Fixed `StatisticsJob` overwriting `Failed` state with `Succeeded`
- Fixed `MovieStore` Pinia getter mutating state (infinite reactivity loops)
- Fixed inverted sort direction and swapped Hangfire queue names
- Fixed `File.OpenWrite` not truncating existing files on overwrite
- Fixed various frontend bugs: settings parser crash, onboarding toggle, ToggleButton boolean handling

### Reliability
- Eliminated `async void` event handlers (unhandled exceptions crashed the process)
- Patched EventSource and SignalR memory leaks on reconnect/unmount
- Fixed `HttpClient` leak in `OpenAiService.GetModels()`
- Replaced `Console.WriteLine` with structured `ILogger` calls

### Features
- **Multi-service translation chain** — configure multiple translation services in priority order with drag-to-reorder; jobs automatically fall through the chain on errors or quota exhaustion
- **Per-service monthly character quotas** — set optional character limits per service with usage tracking and progress bars
- **Dashboard quota widget** — view current month's usage per service at a glance
- Improved webhook setup instructions with authentication guidance
- Bumped default concurrent jobs support (`MAX_CONCURRENT_JOBS`)

### Infrastructure
- Added `/api/health` endpoint with DB connectivity check
- Added `HEALTHCHECK` and `EXPOSE 9876` to Dockerfile
- Fixed Client Dockerfile CMD syntax; pinned to `node:22-alpine`
- Centralized config path resolution with fallback chain
- Added GitHub Actions CI for multi-arch Docker builds to GHCR

### Performance
- `CleanupJob` now uses `ExecuteDeleteAsync()` instead of loading all rows into memory

---

## Contributing

See [Contributing Guidelines](CONTRIBUTING.md) for development setup and coding standards.

## Credits

Icons: [Lucide](https://lucide.dev/icons) | Subtitle Parsing: [AlexPoint](https://github.com/AlexPoint/SubtitlesParser) | Translation: [LibreTranslate](https://libretranslate.com) | [GTranslate](https://github.com/d4n3436/GTranslate)

Special thanks: [selfh.st by Ethan](https://selfh.st/?ref=lingarr), [r/selfhosted](https://www.reddit.com/r/selfhosted/), [FrankieBBBB](https://github.com/FrankieBBBB)
