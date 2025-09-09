namespace WasmClient.Constants
{
    public static class FieldConstants
    {
        public const int FieldWidth = 200; // Width of the battle field in pixels
        public const int FieldHeight = 200; // Height of the battle field in pixels
        public const int GridSize = 20; // Number of grid cells in each dimension
        public const int CellSize = FieldWidth / GridSize; // Size of each grid cell in pixels

        public const int MaxEntities = 10; // Maximum number of entities on the field
        public const int EntitySize = 8; // Size of entities in pixels
    }
}