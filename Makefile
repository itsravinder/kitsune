# ============================================================
# KITSUNE – Makefile
# Usage: make <target>
# ============================================================

.PHONY: help setup run-all stop backend ai ui docker clean db-setup git-push

GITHUB_REPO ?= https://github.com/itsravinder/kitsune.git

help: ## Show available commands
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-22s\033[0m %s\n", $$1, $$2}'

# ── Setup ─────────────────────────────────────────────────────
setup: ## Install all dependencies (dotnet restore, pip install, npm install)
	cd backend     && dotnet restore
	cd ai-service  && pip install -r requirements.txt
	cd ui          && npm install
	@echo "✓ All dependencies installed"

models: ## Pull Ollama AI models
	ollama pull defog/sqlcoder
	ollama pull qwen3-coder:480b-cloud
	@echo "✓ Models pulled"

# ── Run individual services ────────────────────────────────────
backend: ## Start .NET backend (port 5000)
	cd backend && dotnet run

ai: ## Start Python AI service (port 8000)
	cd ai-service && uvicorn main:app --host 0.0.0.0 --port 8000 --reload

ui: ## Start React UI (port 3000)
	cd ui && npm start

# ── Run everything via Docker ──────────────────────────────────
docker-up: ## Start all services with Docker Compose
	docker compose up -d
	@echo "✓ All services starting..."
	@echo "  UI:      http://localhost:3000"
	@echo "  Backend: http://localhost:5000/swagger"
	@echo "  AI:      http://localhost:8000/docs"

docker-down: ## Stop all Docker services
	docker compose down

docker-build: ## Rebuild all Docker images
	docker compose build --no-cache

docker-logs: ## Stream logs from all services
	docker compose logs -f

# ── Database ───────────────────────────────────────────────────
db-setup: ## Run backend to create all tables (then stop)
	cd backend && dotnet run &
	sleep 5
	curl -s http://localhost:5000/health
	@echo "\n✓ Tables created. Stop the server with Ctrl+C."

# ── Build ──────────────────────────────────────────────────────
build-backend: ## Build .NET backend for production
	cd backend && dotnet publish -c Release -o ../dist/backend

build-ui: ## Build React UI for production
	cd ui && npm run build

build-all: build-backend build-ui ## Build all services for production
	@echo "✓ Production builds ready in dist/"

# ── Git ────────────────────────────────────────────────────────
git-push: ## Add, commit, and push all changes
	git add -A
	git commit -m "chore: update $(shell date -u +%Y-%m-%dT%H:%M:%SZ)"
	git push origin main

git-setup: ## Configure git remote and push first time
	git remote add origin $(GITHUB_REPO) 2>/dev/null || git remote set-url origin $(GITHUB_REPO)
	git push -u origin main

# ── Cleanup ────────────────────────────────────────────────────
clean: ## Remove build artifacts
	rm -rf backend/bin backend/obj dist/
	rm -rf ui/build ui/node_modules/.cache
	find . -name "__pycache__" -type d -exec rm -rf {} + 2>/dev/null || true
	@echo "✓ Cleaned"

# ── Health checks ──────────────────────────────────────────────
health: ## Check health of all running services
	@echo "Backend:"  && curl -s http://localhost:5000/health        | python3 -m json.tool 2>/dev/null || echo "  Not running"
	@echo "AI Service:" && curl -s http://localhost:8000/health      | python3 -m json.tool 2>/dev/null || echo "  Not running"
	@echo "UI:"        && curl -s -o /dev/null -w "  Status: %{http_code}\n" http://localhost:3000 || echo "  Not running"

# ── Regenerate setup scripts ────────────────────────────────────
regen-scripts: ## Regenerate setup-kitsune.ps1 and .sh with latest files
	python3 -c "\
import os,base64; ROOT='.'; \
files=[(os.path.relpath(os.path.join(d,f),ROOT).replace(chr(92),'/'), open(os.path.join(d,f),'rb').read()) \
  for d,_,fs in os.walk(ROOT) for f in fs \
  if '.git/' not in os.path.join(d,f) and 'setup-kitsune' not in f and 'node_modules' not in os.path.join(d,f)]; \
print(f'Would embed {len(files)} files'); \
"
	@echo "Run the Python script in the project root to regenerate setup scripts"
