# Коммиты для первой заливки на GitHub

Выполняйте команды по порядку из корня папки `shutdown-timer`. Каждый коммит добавляет только свои файлы, поэтому история получается чистой. Проект собирается после каждого коммита.

Перед началом замените в файлах:

- `YOUR_USERNAME` на ваш логин GitHub (`README.md`, `CHANGELOG.md`)
- `YOUR NAME` на ваше имя (`LICENSE`)

## 0. Инициализация

```bash
cd shutdown-timer
git init -b main
```

Если git ещё не знает, кто вы:

```bash
git config --global user.name "Ваше Имя"
git config --global user.email "you@example.com"
```

## 1. Служебные файлы репозитория

```bash
git add .gitignore .editorconfig LICENSE
git commit -m "chore: add .gitignore, .editorconfig and MIT license"
```

## 2. Само приложение

```bash
git add src/
git commit -m "feat: add Shutdown Timer WPF application" \
           -m "Timer and exact-time modes, shutdown or restart action, reminders at 10/5/1 min and 30 s, 20 s window to cancel, system tray support and animated dark UI."
```

## 3. Документация для разработчиков

```bash
git add docs/ CONTRIBUTING.md CHANGELOG.md
git commit -m "docs: add architecture notes, contributing guide and changelog"
```

## 4. README

```bash
git add README.md
git commit -m "docs: add README with installation and usage instructions"
```

## 5. CI и релизы

```bash
git add .github/workflows/
git commit -m "ci: add build and release workflows" \
           -m "Build runs on push and pull requests to main. Pushing a v* tag publishes a self-contained single-file exe and attaches it to a GitHub release."
```

## 6. Шаблоны issue и pull request

```bash
git add .github/ISSUE_TEMPLATE/ .github/PULL_REQUEST_TEMPLATE.md
git commit -m "chore: add issue and pull request templates"
```

Проверка, что ничего не забыто (список должен быть пустым):

```bash
git status
git log --oneline
```

## 7. Публикация

Создайте на GitHub пустой репозиторий `shutdown-timer` (без README, .gitignore и лицензии, они уже есть). Затем:

```bash
git remote add origin https://github.com/YOUR_USERNAME/shutdown-timer.git
git push -u origin main
```

## 8. Первый релиз (по желанию)

Тег запустит workflow `Release`, который соберёт `ShutdownTimer-win-x64.zip` и приложит его к релизу:

```bash
git tag -a v0.1.0 -m "Shutdown Timer 0.1.0"
git push origin v0.1.0
```

## Как называть коммиты дальше

Используется формат Conventional Commits, вот шаблоны на будущее:

| Что сделали | Пример |
|---|---|
| Новая функция | `feat(timer): add hibernate action` |
| Исправление | `fix(tray): remove icon on exit` |
| Документация | `docs: describe settings file` |
| Рефакторинг | `refactor: extract ShutdownScheduler from MainWindow` |
| Тесты | `test: cover next-clock-time calculation` |
| Зависимости, сборка | `build: bump target framework` |
| Выпуск версии | `chore(release): 0.2.0` |
