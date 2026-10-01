# Контракт беты Xrace

Сцены: `Garage` (стартовая), `Touge01` (гонка). UI: Unity UI + TextMeshPro.
Связи через serialized-поля, без GameObject.Find. Editor-скрипты: Assets/Scripts/Editor/.

## Сцены и объекты

- GarageRoot, CarSpot, GarageCamera, Canvas, Btn_Race, CarName_Text.
- RaceRoot, StartSpawn (311, 152, -156), FinishTrigger (-372, 40, 190).
- DriftZones/DriftZone_01, DriftZone_02, ...
- HUD_Panel: Time_Text, Score_Text, Multiplier_Text.
- Finish_Panel: FinishTime_Text, FinishScore_Text, Record_Text, Btn_Garage, Btn_Retry. По уточнению этапа 6 таблица результатов не требуется в бете.
- Pause_Panel: Btn_Continue, Btn_Settings, Btn_ExitGarage.
- Settings_Panel: MusicVolume_Slider, Btn_MusicPrev, Btn_MusicNext, Btn_SettingsBack.

## Этап 2: данные

Классы без namespace, как существующие скрипты проекта.
CarDefinition: carId (уникальный стабильный ID), displayName, carPrefab, previewImage (Sprite), power, grip, weight, topSpeed.
SelectedCarService.Set(CarDefinition)/Get()/Clear(): выбор только в памяти, сохраняется между сценами.
ResultsRepository.Load() возвращает ResultsData с results, сортированными по driftScore DESC, timeSeconds ASC; при полном равенстве — id ASC.
ResultsRepository.SaveNewEntry(RaceResultEntry) вызывается ровно один раз при финише.
JSON: Application.persistentDataPath/race_results.json, schemaVersion = 1.
Поля записи: id, carId, carName, timeSeconds, driftScore, maxMultiplier, createdAtUtc.
Пустые id/createdAtUtc автоматически заполняются GUID/UTC ISO 8601.
SaveResult: resultId, place (с 1), isNewRecord (очки строго больше прошлого максимума).
Пустая история имеет базовый максимум 0: первый положительный результат — рекорд, нулевой — нет.
Повторный id отклоняется. Битый JSON переносится в race_results_corrupt_<UTC timestamp>.json, возвращается пустая история без записи нового JSON до финиша.
Ошибки доступа/записи и неизвестная версия схемы передаются вызывающему коду: финиш должен показать ошибку сохранения, а не успех.
Запись через временный файл в persistentDataPath и атомарную замену; результаты не стираются перед записью.
Текущий заезд в будущем окне результатов показывается отдельно сверху и выделяется по resultId; сохранённый список остаётся отсортированным.

## Этап 2: музыка и настройки

По уточнению этапа 7 музыка запускается при загрузке Touge01, в Garage остановлена. При повторном запуске карты выбранный трек начинается сначала.
Выделенный корневой GameObject MusicManager в Garage: MusicManager + AudioSource; singleton DontDestroyOnLoad.
tracks: AudioClip[] в порядке переключения. Пустые слоты пропускаются, пустой список безопасен. Каждый трек повторяется до ручного переключения.
NextTrack()/PrevTrack() переключают циклически; кнопки вызывают эти методы.
Клавиши RightArrow/LeftArrow для музыки разрешены только в Touge01 при racePaused. В гараже стрелки выбирают машины.
Контроллер паузы обязан вызвать MusicManager.Instance.SetRacePaused(true) при открытии и false при продолжении/финише/выходе. При смене активной сцены флаг сбрасывается.
Во время активной гонки Update выходит до чтения клавиатуры; нет поиска сцен, загрузки ресурсов или управляемых аллокаций каждый кадр.
Поддерживается Legacy Input (KeyCode) и New Input System при отключённом Legacy.
SettingsService.Load()/Save()/Get()/Set(float): PlayerPrefs MusicVolume, диапазон 0..1, default 0.8.
Set уведомляет MusicVolumeChanged, Save сохраняет PlayerPrefs. MusicManager.SetVolume(float) делает оба вызова; подключить к Slider.OnValueChanged (Dynamic float), диапазон 0..1.
При открытии настроек будущий UI устанавливает Slider через SetValueWithoutNotify(SettingsService.Get()).
Музыка продолжает играть во время паузы. AudioSource настроен на 2D, loop и ignoreListenerPause.

## Остальной бета-скоуп

Без отсчёта. Таймер и дрифт стартуют при скорости выше порога; пауза останавливает таймер.
Последовательные повороты с дрифтом повышают множитель; поворот без дрифта сбрасывает до x1, накопленные очки сохраняются.
Финиш останавливает машину и показывает время, очки и НОВЫЙ РЕКОРД при isNewRecord.
В ГАРАЖ загружает Garage, ИГРАТЬ перезапускает Touge01. По уточнению этапа 6 таблица результатов в бете отсутствует.
Единственная настройка — громкость музыки.

## Этап 3: гараж

Assets/Scenes/Garage.unity — сцена 0; Assets/Scenes/Touge01.unity — сцена 1.
GarageManager на GarageRoot: cars, carSpot, carNameText, musicManagerPrefab — serialized-ссылки.
Canvas содержит Btn_PrevCar, Btn_NextCar, CarName_Text, Btn_Race. Button.OnClick связан соответственно с PrevCar, NextCar, Race.
CarDefinition-ассеты: Assets/ScriptableObjects/Cars/ae86.asset и rmcar26.asset; используются готовые Assets/Prefabs/Toyota AE86 Sprinter Trueno Zenki.prefab и Assets/Prefabs/Car.prefab.
В CarSpot создаётся только одно визуальное превью. Скрипты машины удаляются только с экземпляра превью, камеры/слушатели/звук/коллайдеры отключены, Rigidbody кинематический; исходные префабы не меняются.
LeftArrow/RightArrow в Garage выбирают машину и не переключают музыку одновременно.
Race сохраняет CarDefinition в SelectedCarService и асинхронно загружает Touge01; спавн машины на трассе — следующий этап.
MusicManager уже присутствует в сцене. При его отсутствии GarageManager создаёт экземпляр из Assets/Prefabs/Systems/MusicManager.prefab (с сохранением назначенных треков).
Assets/Audio/Music — папка для пользовательских треков; на момент генерации музыкальных клипов в проекте нет. Назначать Tracks в сцене и в префабе MusicManager для резервного создания.
GarageSceneBuilder (Assets/Scripts/Editor): Xrace/Create Beta Garage; создаёт исходную сцену и отказывается перезаписывать существующий гараж.
GarageSmokeCheck.Run — batch Play Mode проверка настоящих OnClick, смены превью, безопасности превью и загрузки Touge01 с сохранённым выбором.

## Этап 4: гоночное ядро

RaceRoot содержит CarSpawner, RaceSessionManager, RaceTimer, VehicleDriftStateAdapter, DriftScorer, CarStopOnFinish.
CarSpawner: StartSpawn, fallbackCar (исходная сценная AE86), sceneVehicleObjects (старая машина/камеры), session — serialized. Выбранный префаб создаётся один раз; старая машина и её камеры отключаются. Повторный Spawn возвращает уже связанную машину. SpawnedRaceCar помечает активный автомобиль. Без выбора используется сценная AE86, перемещённая на StartSpawn.
Ready -> Racing при горизонтальной скорости строго выше startSpeedKmh (5): вертикальное падение при спавне не запускает таймер. Racing -> Finished один раз через RaceFinishTrigger; пауза запрещает старт/финиш и начисление.
RaceTimer считает Time.deltaTime только в Racing, без паузы, формат mm:ss.fff. SetPaused(bool) должен вызываться будущим контроллером паузы вместе с Time.timeScale.
VehicleDriftStateAdapter читает WheelController.SpeedKph/DriftAngle и grounded у WheelCollider; fallback — Rigidbody и проверка земли лучом. Пороги 25 км/ч, 12 градусов; разворот >=85 градусов и движение назад не считаются дрифтом.
DriftZone: zoneId (с 1 без пропусков), minDriftTime=0.35, minSpeedKmh=25, session, scorer; BoxCollider IsTrigger=true, без Rigidbody. Коллайдеры машины учитываются набором: зона заканчивается только после выхода последнего коллайдера.
DriftScorer учитывает только одну активную зону и ожидает возрастающий zoneId. Повтор старой зоны игнорируется; пропуск зоны сбрасывает цепочку и начинает новую на следующей встреченной зоне. Пересечения боксов недопустимы.
Множитель в зоне = successfulChain+1. Успех — суммарный валидный дрифт в зоне >=minDriftTime, выход повышает цепочку; неуспех сбрасывает цепочку/x1, очки сохраняются. Короткий дрифт начисляет очки, но не повышает цепочку.
Очки = 100 * clamp01(abs(slip)/45) * clamp01(kmh/80) * multiplier * fixedDeltaTime. В интерфейсе показывается целая часть; дроби сохраняются внутри расчёта. MaxMultiplier хранит максимальный реально использованный множитель.
События: OnScoreChanged(int), OnMultiplierChanged(int), OnZoneSuccess(int zoneId), OnChainReset().
На финише управление WheelController отключается, motorTorque=0, применяется тормоз колёс; velocity и angularVelocity плавно гасятся за 0.75 сек, затем isKinematic=true. Подвеска/сцепление и WheelCollider не изменяются.
RaceCanvas/HUD_Panel: Time_Text, Score_Text, Multiplier_Text. С этапа 5 контроллер RaceHUDController находится на RaceCanvas, вне скрываемой панели.
Автоматически размещены 19 зон по изгибам дорожного меша. Их границы нужно проверить ручным проездом, особенно на соседних витках серпантина. Разметка не заменяет художественную настройку поворотов.
Старт (311,152,-156), финиш (-372,40,190); направления взяты из центральной линии дороги. Бокс финиша 20x5x4.
Сохранение JSON и окно финиша в этом этапе ещё не подключены; результат доступен через RaceTimer.TimeSeconds, DriftScorer.Score/MaxMultiplier и событие RaceSessionManager.OnStateChanged.

## Этап 5: HUD

Assets/Scripts/UI/RaceHUDController.cs на RaceCanvas: session, timer, scorer (компоненты RaceRoot), hudPanel, timeText, scoreText, multiplierText — serialized-ссылки.
RaceTimer.OnTimeChanged(float timeSeconds) вызывается при изменении времени; HUD подписывается на него, DriftScorer.OnScoreChanged/OnMultiplierChanged и RaceSessionManager.OnStateChanged в OnEnable, отписывается в OnDisable.
Начальные значения и значения после повторного включения берутся из текущего состояния заезда. Время mm:ss.fff (минуты не оборачиваются после 59), очки «Дрифт: 12450», множитель «x2».
Контроллер не имеет Update: числа формируются в переиспользуемых char[] и передаются TMP.SetCharArray без конкатенаций/ToString/string.Format. Буферы и TMP вместимость прогреваются при включении. Это не обещание отсутствия всех аллокаций сторонних скриптов или всего Canvas.
В установленной версии TMP SetCharArray внутри UNITY_EDITOR создаёт строку для инспектора; эта ветка отсутствует в Player. Проверка GC в редакторе должна учитывать это поведение TMP.
HUD_Panel активна в Ready/Racing, скрыта в Finished. Контроллер остаётся включён на Canvas; HUD CanvasGroup не блокирует raycasts, тексты не перехватывают клики.
Расположение, размеры, шрифт, цвет и выравнивание TMP контроллер не меняет. Score Prefix в инспекторе (по умолчанию «Дрифт: ») задаёт подпись перед очками; менять вне Play Mode. Поля Text заполняются данными во время игры.
RaceHUDEditorSetup (Assets/Scripts/Editor), меню Xrace/Upgrade Race HUD, настраивает ссылки в существующем HUD, сохраняя RectTransform и оформление.

## Этап 6: финишное окно

FinishPanelController на RaceCanvas, вне Finish_Panel. Serialized: session/timer/scorer (RaceRoot), fallbackCar (ae86.asset для прямого запуска сцены), finishPanel, finishTimeText, finishScoreText, recordText, saveStatusText, garageButton, retryButton.
Finish_Panel и Record_Text сохранены неактивными; панель показывается только при Finished. Время mm:ss.fff, очки «Дрифт: N», Record_Text «НОВЫЙ РЕКОРД» показывается только после успешной записи со строго большим driftScore.
В обработчике Finished ровно один раз создаётся GUID, UTC ISO 8601 дата, сохраняются машина из SelectedCarService (резервно — fallbackCar), время, очки, maxMultiplier через ResultsRepository.SaveNewEntry. Повторный финиш/повторное включение UI не создают дубликат.
LastResult, SavedResult, SaveSucceeded доступны для проверки. Ошибки хранения отображаются в SaveStatus_Text, рекорд при ошибке скрыт; кнопки доступны независимо от сохранения.
Btn_Garage.OnClick -> FinishPanelController.GoGarage(); Btn_Retry.OnClick -> Retry(). Перед загрузкой Time.timeScale=1, AudioListener.pause=false, SetPaused(false), повторные клики блокируются. Выбор машины сохраняется для перезапуска.
Финишное окно не ставит Time.timeScale=0: машина успевает затормозить за 0.75 сек, таймер уже остановлен состоянием Finished.
RaceCanvas содержит GraphicRaycaster, в сцене создан EventSystem с InputSystemUIInputModule. Фон финиша перехватывает клики; TMP-тексты RaycastTarget=false.
Оформление редактируется RectTransform/TMP/Button/Image в Unity. Контроллер не меняет размеры, позиции, цвета и шрифты. FinishPanelEditorSetup создаёт панель один раз и отказывается перезаписывать существующую.
FinishPanelSmokeCheck проверяет три заезда (рекорд/равенство/новый рекорд), сохранение один раз, значения текстов, две перезагрузки и возврат в Garage. Исходная история временно переносится в резервный файл и восстанавливается после проверки.
Таблица результатов, кнопка результатов и зависимость от таблицы отсутствуют по уточнению этапа 6. Следующий шаг — пауза и настройки громкости.

## Этап 7: пауза и громкость

PauseMenuController на RaceCanvas (вне скрываемых панелей). Serialized: session из RaceRoot, pausePanel, settingsPanel, musicVolumeSlider, trackNameText, musicManagerPrefab.
Pause_Panel: Btn_Continue, Btn_Settings, Btn_ExitGarage. Settings_Panel: MusicVolume_Slider (0..1), Btn_MusicPrev, Btn_MusicNext, TrackName_Text, Btn_SettingsBack. Обе панели изначально выключены.
Кнопки привязаны к Continue/OpenSettings/ExitGarage/PrevTrack/NextTrack/SettingsBack; Slider.OnValueChanged Dynamic float -> SetMusicVolume.
ESC переключает паузу только в Touge01, в Ready/Racing; после Finished игнорируется. Код не вызывает Application.Quit. New Input System приоритетен, Legacy KeyCode используется при отсутствии New Input System.
Открытие: Time.timeScale=0, session.SetPaused(true), AudioListener.pause=true, курсор освобождается. Музыка ignoreListenerPause=true продолжает играть. Продолжение возвращает timeScale=1, звуки и прежнее состояние курсора.
Настройки не снимают паузу. Громкость загружается через SetValueWithoutNotify, изменение применяет SettingsService.Set/Save и AudioSource.volume. Стрелки листают треки через MusicManager; Navigation=None у слайдера/кнопок предотвращает двойное действие стрелок.
При прямом запуске Touge01 менеджер создаётся из Assets/Prefabs/Systems/MusicManager.prefab; в Garage используется существующий менеджер. MusicManager в Garage является экземпляром этого префаба: настройте tracks один раз в префабе. Сейчас музыкальные клипы не добавлены; звуки машины не используются вместо музыки.
Для оформления временно включите нужную панель вне Play Mode, меняйте RectTransform/TMP/Image, затем выключите обе панели и сохраните Touge01. Контроллер оставьте включённым на RaceCanvas.
Editor: Xrace/Create Pause And Settings Panels создаёт начальную разметку; повторное создание блокируется, чтобы сохранить ручной дизайн. PauseMenuSmokeCheck.Run проверяет паузу, таймер, громкость, треки и переход в гараж, возвращает исходную пользовательскую громкость после теста.