namespace Shared.Common;

/// <summary>
/// ランダムなプレイヤー名を生成するサービス
/// </summary>
public static class PlayerNameGenerator
{
    private static readonly string[] FirstNames = {
        "Lightning", "Shadow", "Fire", "Ice", "Storm", "Wind", "Earth", "Water",
        "Thunder", "Flame", "Frost", "Gale", "Stone", "Wave", "Spark", "Mist",
        "Solar", "Lunar", "Star", "Nova", "Phoenix", "Dragon", "Tiger", "Wolf",
        "Eagle", "Hawk", "Lion", "Bear", "Fox", "Raven", "Falcon", "Viper"
    };

    private static readonly string[] LastNames = {
        "Blade", "Shield", "Arrow", "Hammer", "Spear", "Bow", "Sword", "Axe",
        "Staff", "Wand", "Crown", "Wing", "Claw", "Fang", "Heart", "Soul",
        "Spirit", "Knight", "Warrior", "Mage", "Archer", "Guardian", "Hunter",
        "Ranger", "Paladin", "Rogue", "Monk", "Sage", "Wizard", "Sorcerer", "Hero"
    };

    private static readonly Random _random = new();

    /// <summary>
    /// ランダムなプレイヤー名を生成する
    /// </summary>
    /// <returns>Generated player name in format "FirstName LastName"</returns>
    public static string GenerateRandomName()
    {
        var firstName = FirstNames[_random.Next(FirstNames.Length)];
        var lastName = LastNames[_random.Next(LastNames.Length)];
        return $"{firstName} {lastName}";
    }

    /// <summary>
    /// 短縮版のプレイヤー名を生成する（表示用）
    /// </summary>
    /// <returns>Generated short player name in format "FirstLast"</returns>
    public static string GenerateShortName()
    {
        var firstName = FirstNames[_random.Next(FirstNames.Length)];
        var lastName = LastNames[_random.Next(LastNames.Length)];
        return $"{firstName}{lastName}";
    }

    /// <summary>
    /// シード付きでランダムなプレイヤー名を生成する（テスト用）
    /// </summary>
    /// <param name="seed">Random seed</param>
    /// <returns>Generated player name</returns>
    public static string GenerateRandomName(int seed)
    {
        var random = new Random(seed);
        var firstName = FirstNames[random.Next(FirstNames.Length)];
        var lastName = LastNames[random.Next(LastNames.Length)];
        return $"{firstName} {lastName}";
    }
}
