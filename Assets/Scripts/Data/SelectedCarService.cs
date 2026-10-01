public static class SelectedCarService
{
    private static CarDefinition selectedCar;
    public static void Set(CarDefinition car) => selectedCar = car;
    public static CarDefinition Get() => selectedCar;
    public static void Clear() => selectedCar = null;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Clear();
}
