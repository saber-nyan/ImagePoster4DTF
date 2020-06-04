# <p align="center">ImagePoster4DTF</p>
Утилита для массовой загрузки изображений в черновики для DTF.ru. По просьбе [@Knightmare](https://dtf.ru/u/132253-knightmare) и в целях прочего щитпостинга.

<p align="center">
<img src="https://user-images.githubusercontent.com/25345196/83816463-1b036280-a6cb-11ea-8400-f94e9150713d.png" alt="Скриншот софтины."></img>
</p>

## Запуск
[![GitHub All Releases](https://img.shields.io/github/downloads/saber-nyan/ImagePoster4DTF/total?color=red&style=for-the-badge)](https://github.com/saber-nyan/ImagePoster4DTF/releases/latest)

1. Скачайте последний релиз (и `pdb` файл) по кнопке выше.
2. Положите `.exe` и `.pdb` файл в одном каталоге -- это поможет при отладке ошибок.
3. Запустите приложение, введите свою почту и пароль как на DTF.
4. После входа в каталоге приложения создастся файл `dtf_settings.ini`. Не передавайте и никому не показывайте его содержимое,
иначе злоумышленник сможет воспользоваться вашим аккаунтом.
5. Выберите путь до директории откуда будут браться изображения или файлы вручную. 
**ВАЖНО:** при указании директории она сканируется рекурсивно; это значит, что изображения в подкаталогах и ниже тоже будут загружены.
6. Опционально: назовите свой черновик.
7. Нажмите кнопку "Загрузить!" и ожидайте загрузки. По окончании будет открыт черновик в браузере по умолчанию.

О любых проблемах сообщайте в [баг-трекер](https://github.com/saber-nyan/ImagePoster4DTF/issues).

## Сборка
Должен быть установлен dotnet-cli, .NET Core 3 SDK и Git.
```cmd
git clone https://github.com/saber-nyan/ImagePoster4DTF.git
cd ImagePoster4DTF\ImagePoster4DTF
dotnet publish -c Release -r win-x64
```

Готовый бинарь со всеми зависимостями будет находиться по пути `ImagePoster4DTF\bin\Release\netcoreapp3.1\win-x64\publish\` относительно
корневой директории проекта.

## TODO
- [x] Базовые возможности
	- [x] Вход в аккаунт
	- [x] Постинг картинок из директории
	- [x] Постинг выбранных вручную картинок
	- [ ] Выбор директории файлпикером
- [ ] Продвинутая обработка ошибок, проверка кода каждого запроса в JSON
- [ ] Маркировка поста тэгом `#thisPostWasMadeByOchobaHatersGang` и ссылкой на этот репозиторий (опционально)
- [ ] Создание подписи из имени файла (замена по регэксу?)
- [ ] Переезд на другой язык из-за кросс-платформенности?

## Зависимости, лицензии
| Зависимость | Лицензия |
|-|-|
| [Flurl](https://github.com/tmenier/Flurl) | MIT |
| [ini-parser](https://github.com/rickyah/ini-parser) | MIT |
| [MaterialDesignThemes](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) | MIT |
| [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) | MIT |

![License: WTFPL](https://img.shields.io/badge/license-WTFPL-blue?style=for-the-badge)

*desu~*
