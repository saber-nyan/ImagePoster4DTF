# <p align="center">ImagePoster4DTF</p>
[![Статус сборки](https://travis-ci.com/saber-nyan/ImagePoster4DTF.svg?branch=master)](https://travis-ci.com/saber-nyan/ImagePoster4DTF)

Утилита для массовой загрузки изображений в черновики для DTF.ru. По просьбе [@Knightmare](https://dtf.ru/u/132253-knightmare) и в целях прочего щитпостинга.

<p align="center">
<img src="https://user-images.githubusercontent.com/25345196/86230794-40846e80-bb9a-11ea-925f-4b0681697024.png" alt="Скриншот софтины."></img>
</p>

## Запуск
[![GitHub All Releases](https://img.shields.io/github/downloads-pre/saber-nyan/ImagePoster4DTF/total?color=red&style=for-the-badge)](https://github.com/saber-nyan/ImagePoster4DTF/releases/latest)

1. Скачайте последний релиз по кнопке выше.
2. Если находитесь под GNU/Linux, до дайте права на запуск: `chmod +x ./ImagePoster4DTF_linux`
3. Запустите приложение, введите свою почту и пароль как на DTF или следуйте [инструкции по вытаскиванию Cookie](#вход-по-cookie).
4. После входа в каталоге приложения создастся файл `dtf_settings.ini`. Не передавайте и никому не показывайте его содержимое,
иначе злоумышленник сможет воспользоваться вашим аккаунтом.
5. Выберите путь до директории откуда будут браться изображения или файлы вручную.<br/>
При необходимости выключите рекурсивную загрузку (сканирование подкаталогов).
6. Опционально: напишите [регулярное выражение](#регулярные-выражения) для создания заголовка из имени файла.
7. Опционально: назовите свой черновик.
8. Нажмите кнопку "Загрузить!" и ожидайте загрузки. По окончании будет открыт черновик в браузере по умолчанию.

О любых проблемах сообщайте в [баг-трекер](https://github.com/saber-nyan/ImagePoster4DTF/issues).
При этом желательно приложить лог, создающийся в каталоге рядом с исполняемым файлом, называющимся как `ImagePoster4DTF_<дата_и_время>.log`.

### Вход по cookie
Для входа по Cookie необходимо вытащить строку из браузера, в котором выполнен вход в аккаунт.<br/>
Ниже показано, как это сделать. Нужна именно кука `osnova-remember`.

<p align="center">
<img src="https://user-images.githubusercontent.com/25345196/86235090-94925180-bba0-11ea-8f49-364616cd61c1.png" alt="Получение cookie."></img>
</p>

### Регулярные выражения
Фича была добавлена для гатарищипостинга, конкретно для вытаскивания времени кадра из имени файла в подпись к картинке.<br/>
Для тестирования регэкса рекомендую [этот сайт](https://regex101.com/).
#### Мой пример
Исходный:
```regexp
At (\d+)_(\d+)_(\d+)\.(\d+).*
```
Замена:
```regexp
[$2:$3]
```
Имя файла `At 00_00_51.802.png` превращается в заголовок `[00:51]`.


## Сборка
Должен быть установлен dotnet-cli, .NET Core 3 SDK и Git.
```cmd
git clone https://github.com/saber-nyan/ImagePoster4DTF.git
cd ImagePoster4DTF\ImagePoster4DTF
dotnet publish -r win-x64 --configuration Release -p:PublishSingleFile=true
```

Готовый бинарь со всеми зависимостями будет находиться по пути `ImagePoster4DTF\bin\Release\netcoreapp3.1\win-x64\publish\` относительно
корневой директории проекта.

## TODO
- [x] Базовые возможности
	- [x] Вход в аккаунт
	- [x] Постинг картинок из директории
	- [x] Постинг выбранных вручную картинок
	- [x] Выбор директории файлпикером
- [x] Продвинутая обработка ошибок, проверка кода каждого запроса в JSON
- [x] Маркировка поста тэгом `#thisPostWasMadeByOchobaHatersGang` и ссылкой на этот репозиторий (опционально)
- [x] Создание подписи из имени файла (замена по регэксу?)
- [x] Переезд на другой язык из-за кросс-платформенности?

## Лицензия

![License: WTFPL](https://img.shields.io/badge/license-WTFPL-blue?style=for-the-badge)

*desu~*
