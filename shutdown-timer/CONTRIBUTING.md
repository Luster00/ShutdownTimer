# Участие в разработке

Спасибо за интерес к проекту. Ниже коротко о том, как предложить изменение.

## Что нужно для работы

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022, Rider или VS Code с C# Dev Kit

Проверить, что всё собирается:

```powershell
dotnet build src/ShutdownTimer -c Release
dotnet run --project src/ShutdownTimer
```

## Как предложить изменение

1. Создайте issue или найдите существующее, чтобы обсудить идею.
2. Сделайте форк и создайте ветку от `main`: `feat/sleep-action`, `fix/tray-tooltip`.
3. Внесите изменения небольшими коммитами.
4. Убедитесь, что проект собирается без предупреждений и приложение запускается.
5. Откройте Pull Request и заполните шаблон.

## Сообщения коммитов

Проект использует [Conventional Commits](https://www.conventionalcommits.org/ru/):

```
<тип>(<область>): <краткое описание в повелительном наклонении>
```

Типы: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`.

Примеры:

```
feat(tray): add hibernate action to tray menu
fix(timer): skip reminders longer than total duration
docs: update build instructions
```

## Стиль кода

- Настройки форматирования лежат в `.editorconfig`.
- Отступ: 4 пробела в C#, 2 пробела в XAML.
- Названия на английском, комментарии и тексты интерфейса могут быть на русском.
- Логику времени по возможности держите отдельно от WPF: так её проще тестировать.

## Проверка перед PR

- [ ] Проект собирается в конфигурации Release
- [ ] Таймер и режим «Точное время» запускаются и отменяются
- [ ] Значок в трее появляется и корректно исчезает при выходе
- [ ] Обновлён `CHANGELOG.md` (раздел `Unreleased`)
- [ ] Обновлена документация, если менялось поведение
