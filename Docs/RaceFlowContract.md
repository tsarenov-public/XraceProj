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
По последнему уточнению музыка останавливается на паузе через AudioSource.Pause и продолжается с той же позиции через UnPause. AudioSource 2D/loop; ignoreListenerPause используется только для явного управления паузой менеджером.

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
В CarSpot создаётся только одно визуальное превью через GaragePreviewBuilder: копируются Transform, MeshFilter/MeshRenderer, SkinnedMeshRenderer с костями и LODGroup со ссылками на копии. Физический префаб не Instantiate-ится в гараже: скрипты машины, Rigidbody, WheelCollider, камеры, AudioSource и ParticleSystem вообще не создаются. Исходные меши/материалы и префабы не меняются.
LeftArrow/RightArrow в Garage выбирают машину и не переключают музыку одновременно.
Race сохраняет CarDefinition в SelectedCarService и асинхронно загружает Touge01; спавн машины на трассе — следующий этап.
MusicManager уже присутствует в сцене. При его отсутствии GarageManager создаёт экземпляр из Assets/Prefabs/Systems/MusicManager.prefab (с сохранением назначенных треков).
Assets/Audio/Music — папка для пользовательских треков; на момент генерации музыкальных клипов в проекте нет. Назначать Tracks один раз в префабе MusicManager для одинаковой работы гаражного экземпляра и резервного создания.
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
Вход в зону сам по себе показывает x1. Только во время валидного дрифта множитель = successfulChain+1; вне дрифта и после выхода показывается x1, успешная цепочка хранится отдельно до следующего поворота. Успех — суммарный валидный дрифт в зоне >=minDriftTime, выход повышает цепочку; неуспех сбрасывает цепочку/x1, очки сохраняются. Короткий дрифт начисляет очки, но не повышает цепочку. Отключение/выгрузка зоны вызывает CancelZone и никогда не засчитывает поворот.
Очки = 100 * clamp01(abs(slip)/45) * clamp01(kmh/80) * multiplier * fixedDeltaTime. В интерфейсе показывается целая часть; дроби сохраняются внутри расчёта. MaxMultiplier хранит максимальный реально использованный множитель.
События: OnScoreChanged(int), OnMultiplierChanged(int), OnZoneSuccess(int zoneId), OnChainReset().
На финише управление WheelController отключается, motorTorque=0, применяется тормоз колёс; velocity и angularVelocity плавно гасятся за 0.75 сек. Затем WheelCollider отключаются перед isKinematic=true; колесные визуальные/звуковые/дымовые скрипты не опрашивают физику кинематического кузова. Параметры подвески и сцепления не меняются; новая гонка создаёт свежую машину.
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
Открытие: Time.timeScale=0, session.SetPaused(true), AudioListener.pause=true, курсор освобождается. MusicManager явно ставит AudioSource на паузу, сохраняет позицию трека; при продолжении вызывает UnPause. Выбор трека в настройках остаётся без звука до продолжения. Продолжение возвращает timeScale=1, звуки и прежнее состояние курсора.
Настройки не снимают паузу. Громкость загружается через SetValueWithoutNotify, изменение применяет SettingsService.Set/Save и AudioSource.volume. Стрелки листают треки через MusicManager; Navigation=None у слайдера/кнопок предотвращает двойное действие стрелок.
При прямом запуске Touge01 менеджер создаётся из Assets/Prefabs/Systems/MusicManager.prefab; в Garage используется существующий менеджер. MusicManager в Garage является экземпляром этого префаба: настройте tracks один раз в префабе. Сейчас музыкальные клипы не добавлены; звуки машины не используются вместо музыки.
Для оформления временно включите нужную панель вне Play Mode, меняйте RectTransform/TMP/Image, затем выключите обе панели и сохраните Touge01. Контроллер оставьте включённым на RaceCanvas.
Editor: Xrace/Create Pause And Settings Panels создаёт начальную разметку; повторное создание блокируется, чтобы сохранить ручной дизайн. PauseMenuSmokeCheck.Run проверяет паузу, таймер, громкость, треки и переход в гараж, возвращает исходную пользовательскую громкость после теста.
## Проверка и исправления перед бетой

GarageManager содержит serialized raceButton: «В ГОНКУ» доступна при активном превью валидной машины/заполненных ссылках/наличии Touge01 в сборке; повторная загрузка защищена. Navigation=None у гаражных стрелок/кнопки гонки.
CarSpawner валидирует Rigidbody до деактивации старой машины, использует fallback и очищает неверный выбранный CarDefinition, чтобы результат сохранял фактическую машину. Устанавливает существующие VehiclePresentation и VehicleHud после динамического спавна, HUD привязывается напрямую к raceSession.
VehicleHud скрывается на паузе, финише и при отключении старой машины. RaceCanvas sortingOrder=200, выше Canvas спидометра.
WheelController не читает управление/передачи в паузе; очередь переключений очищается. Фары и камеры также не реагируют на меню. Физические параметры автомобиля не изменены.
PopupHeadlights использует отдельный материал только для слотов линз своей машины; исходный M_HeadlightLens.mat больше не меняется в Play Mode.
VehicleDriftStateAdapter безопасен до Bind/при удалённых ссылках, не начисляет дрифт задним ходом.
PauseMenuController проверяет ссылки и размещение вне панелей; при отключении компонента снимает паузу и подписки. На финише меню не открывается; ESC не закрывает приложение.
Editor creation tools предлагают сохранить несохранённые ручные сцены перед переключением (в интерактивном Editor). RenderPreview финиша разрешён только в batch mode.
BetaSceneRepair.ApplyAndValidate обновляет только необходимые ссылки/порядок UI/placeholder, сохраняет оформление, затем запускает BetaValidationCheck.Run.
BetaValidationCheck проверяет сцены/ссылки/кнопки/build/input/зоны и JSON/сортировку/рекорды с резервным сохранением исходной истории. RaceCoreSmokeCheck отключает FinishPanelController для тестового финиша и не пишет тестовый заезд в историю. FinishPanelSmokeCheck сохраняет и восстанавливает исходный JSON. PauseMenuSmokeCheck проверяет isPlaying/timeSamples при паузе, продолжении и переключении треков в паузе.

Перед показом остаются ручная проверка ощущения дрифта/зон и поведения на Mac, добавление собственных клипов в prefab MusicManager. В существующей геометрии трассы Unity сообщает о BoxCollider с отрицательным масштабом у некоторых отбойников; автоматические тесты ядра не заменяют проверку столкновений на дороге.

Итоговый регрессионный прогон: BetaValidationCheck, GarageSmokeCheck для AE86 и RMCar26 (включая запуск с рендером), RaceCoreSmokeCheck, FinishPanelSmokeCheck и PauseMenuSmokeCheck прошли. Проверка Mac-сборки и ручное вождение не выполнены на этом Windows-хосте.

## Повторный вход в гонку и оформление гаража

Перед исправлениями удалённый origin проверен через fetch: feat/race-flow-paired совпадала с локальной веткой, новых изменений не было.

Гараж больше не создаёт и не разбирает физический префаб автомобиля: GaragePreviewBuilder строит только визуальную копию. Поэтому в превью нет дыма/следов, автомобильных скриптов, WheelCollider, камер и звуков. LOD и кости скинированного меша ссылаются на новую визуальную иерархию, исходные ассеты не меняются.

GarageRoot/GarageManager: Allow Preview Rotation=true; Preview Rotation Degrees Per Pixel=0.35. Зажать левую кнопку мыши на свободной от UI области и тянуть горизонтально: поворот на полный круг только вокруг локальной оси X превью. Положение/масштаб CarSpot и префаб не меняются. Начало drag на UI не вращает машину; переключение машины сбрасывает угол. Ось X выбрана буквально по запросу пользователя.

Для оформления без Play Mode: открыть Garage, выделить GarageRoot; в Inspector выбрать «Машина для оформления» и нажать «Показать превью в сцене». Менять CarSpot, GarageCamera, освещение, пол и декор. CarPreview_Editor — временный объект, его правки не сохраняются. Он удаляется перед сохранением сцены, запуском Play, закрытием сцены и перезагрузкой скриптов; после сохранения нажать «Показать превью в сцене» снова. «Убрать превью» удаляет его вручную. Новые Inspector-ссылки для этого не нужны.

VehicleRuntimeObjects связывает срок жизни отделённых камер и следов с машиной. Skidmarks явно освобождает созданные Mesh/Material при уничтожении. После финальной остановки WheelCollider отключены, WheelAlignment/WheelSkid/DriftEffects/SkidMarks не читают физику кинематического кузова; VehiclePresentation также не читает отключённый контроллер. Некорректные индексы следов и отсутствующие ссылки обрабатываются безопасно. HUD не обращается к уже уничтоженным TMP/панелям при выгрузке.

В ERProjectLog.prefab EasyRoads очищена ошибочная ссылка на визуальный Material в поле roadPhysicsMaterial: это поле принимает PhysicsMaterial. Геометрия трассы и параметры сцепления машин не пересоздавались.

Skidmarks больше не ищет дорогу через GameObject.Find. CarSpawner на RaceRoot содержит serialized Road Surface, уже связанный с MeshCollider Touge_01 в сцене. При спавне он передаёт ссылку через WheelSkid в контроллеры следов. Если поверхность не назначена, следы используют контакт WheelCollider. Для другой трассы перетащите её дорожный Collider в RaceRoot / CarSpawner / Road Surface. ReentrySmokeCheck проверяет привязку поверхности у выбранных машин, BetaValidationCheck проверяет ссылку сцены.

ReentrySmokeCheck.Run проверяет четыре цикла через реальные кнопки Garage -> Touge01 -> физический FinishTrigger -> Garage, попеременно AE86/RMCar26, отсутствие дублей/ошибок и освобождение native Mesh/Material. Отдельно проверяет удаление камер и следов при уничтожении только машины. Проверка защищает и восстанавливает исходную историю JSON. Основной прогон запускается с графикой, без -nographics.

RaceCoreSmokeCheck дополнительно проверяет проезд без дрифта, настоящий x2, короткий занос без успешной цепочки, воздух/малую скорость/невалидные данные и отмену зоны. Вход, отключение и выгрузка зоны сами по себе не выдают успешный поворот.

В графическом прогоне GarageSmokeCheck с захватом превью воспроизведён native crash в URP GPUResidentDrawer / WorldProcessor / ObjectDispatcher при смене объектов и сцены. В Assets/Settings/PC_RPAsset.asset штатная настройка Rendering / GPU Resident Drawer переведена в Disabled (m_GPUResidentDrawerMode=0); Mobile_RPAsset уже использовал Disabled. BetaValidationCheck защищает эту настройку. Внешний вид URP сохраняется, но работа CPU/draw calls может увеличиться: производительность на Mac нужно проверить в реальном билде. Обновление Unity и пакетов перед бетой не требуется.

## Сброс цепочки при остановке и ударе

RaceRoot / DriftScorer: Stop Speed Kmh=3, Stop Reset Delay=0.25. Во время Racing скорость не выше 3 км/ч непрерывно в течение 0.25 секунды сбрасывает цепочку до x1, в том числе между зонами. Длительное стояние отправляет один сброс; новый сброс разрешается после скорости выше 4 км/ч. Пауза останавливает проверку. Малая скорость сама по себе не даёт очков дрифта.

Collision Reset Delta Speed Mps=1.5 задаёт минимальный импульс удара / массу кузова в м/с; Collision Min Horizontal Normal=0.6 исключает преимущественно вертикальные контакты с дорогой; Collision Reset Cooldown=0.15 исключает повтор одного удара от нескольких контактов. RaceCarCollisionRelay добавляется и привязывается CarSpawner автоматически на Rigidbody выбранной или резервной машины. Новые Inspector-ссылки вручную не нужны. Лёгкое касание, контакт с ровной дорогой, Ready/Finished и пауза цепочку не сбрасывают.

При остановке/ударе successfulChain=0, currentMultiplier=1; весь накопленный Score и MaxMultiplier сохраняются. Время дрифта до события очищается, активная зона остаётся: новый успешный занос в ней начинается с x1 и должен снова длиться минимум Min Drift Time. HUD обновляется через существующие события.

Чтобы остановка срабатывала раньше, увеличьте Stop Speed Kmh либо уменьшите Stop Reset Delay. Чтобы более слабые удары сбрасывали цепочку, уменьшите Collision Reset Delta Speed Mps. Не ставьте нулевые пороги: обычные контакты машины с геометрией могут начать сбрасывать цепочку. Проверяйте на дороге с настоящей физикой машины, включая бордюр и отбойник. RaceCoreSmokeCheck проверяет сохранение очков, одно событие остановки, паузу/дорогу/лёгкое касание и настоящий удар автомобиля в тестовую стену.

После исправлений на Windows / Unity 6000.6.0f1 прошли BetaValidationCheck (ссылки/build/input/JSON/строгие рекорды), GarageSmokeCheck с графикой, альтернативной машиной и захватом превью (тот же прогон, который падал с GPU Resident Drawer), RaceCoreSmokeCheck, FinishPanelSmokeCheck, PauseMenuSmokeCheck и ReentrySmokeCheck с четырьмя графическими циклами на обеих машинах. Тесты результатов восстановили исходную историю. Локальные пользовательские правки материала M_HeadlightLens.mat, .vsconfig и New Scene.unity не менялись и не включены в коммит исправлений. Mac-сборка и ощущение управления/дрифта на трассе требуют ручной проверки на Mac.
