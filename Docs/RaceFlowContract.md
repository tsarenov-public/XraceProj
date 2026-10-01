# Контракт беты Xrace

Сцены: `Garage` (стартовая), `Touge01` (гонка). UI: Unity UI + TextMeshPro.
Связи через serialized-поля, без GameObject.Find. Editor-скрипты: Assets/Scripts/Editor/.

## Сцены и объекты

- GarageRoot, CarSpot, GarageCamera, Canvas, Btn_Race, CarName_Text.
- RaceRoot, StartSpawn (311, 152, -156), FinishTrigger (-372, 40, 190).
- DriftZones/DriftZone_01, DriftZone_02, ...
- HUD_Panel: Time_Text, Score_Text, Multiplier_Text.
- Finish_Panel: FinishTime_Text, FinishScore_Text, Record_Text, Btn_Garage, Btn_Retry, Btn_Results.
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

По уточнению этапа 2 музыка играет и в Garage, и в Touge01, непрерывно при переходах.
Выделенный корневой GameObject MusicManager в Garage: MusicManager + AudioSource; singleton DontDestroyOnLoad.
tracks: AudioClip[] в порядке переключения. Пустые слоты пропускаются, пустой список безопасен. Каждый трек повторяется до ручного переключения.
NextTrack()/PrevTrack() переключают циклически; кнопки вызывают эти методы.
Клавиши RightArrow/LeftArrow разрешены в Garage и в Touge01 только при racePaused.
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
В ГАРАЖ загружает Garage, ИГРАТЬ перезапускает Touge01; Результаты открывает историю.
Единственная настройка — громкость музыки.
