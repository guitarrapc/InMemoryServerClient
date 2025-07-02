namespace BattleLogic;

/// <summary>
/// バトルAIのパラメーター調整用定数
/// </summary>
public static class BattleAIDefines
{
    #region 行動選択の報酬値

    /// <summary>
    /// 隣接する敵への攻撃報酬
    /// </summary>
    public const float AttackAdjacentReward = 30.0f;

    /// <summary>
    /// HPが低い敵への攻撃ボーナス
    /// </summary>
    public const float AttackLowHpBonus = 8.0f;

    /// <summary>
    /// HPが低い時の防御報酬
    /// </summary>
    public const float DefendLowHpReward = 2.0f;

    /// <summary>
    /// 敵が近くにいる場合の防御ボーナス
    /// </summary>
    public const float DefendEnemiesNearbyReward = 1.5f;

    /// <summary>
    /// 最も近い敵への移動報酬
    /// </summary>
    public const float MoveToNearestReward = 15.0f;

    /// <summary>
    /// HPが最も低い敵への移動報酬
    /// </summary>
    public const float MoveToLowestHpReward = 12.0f;

    /// <summary>
    /// 敵を囲む移動の報酬
    /// </summary>
    public const float MoveToSurroundReward = 6.0f;

    /// <summary>
    /// 敵が近くにいると判断する距離の閾値
    /// </summary>
    public const int NearbyDistanceThreshold = 2;

    #endregion

    #region HPに関する閾値

    /// <summary>
    /// HPが危険と判断する閾値（最大HPに対する割合）
    /// </summary>
    public const float CriticalHpRatio = 0.2f;

    /// <summary>
    /// HPが低いと判断する閾値（最大HPに対する割合）
    /// </summary>
    public const float LowHpRatio = 0.3f;

    /// <summary>
    /// HPが十分と判断する閾値（最大HPに対する割合）
    /// </summary>
    public const float SufficientHpRatio = 0.5f;

    /// <summary>
    /// HPが高いと判断する閾値（最大HPに対する割合）
    /// </summary>
    public const float HighHpRatio = 0.7f;

    #endregion

    #region エンティティタイプに関する乗数

    /// <summary>
    /// プレイヤー以外の攻撃性ボーナス乗数
    /// </summary>
    public const float NonPlayerAttackMultiplier = 1.5f;

    /// <summary>
    /// プレイヤー以外の移動攻撃性ボーナス乗数
    /// </summary>
    public const float NonPlayerMoveMultiplier = 1.3f;

    /// <summary>
    /// プレイヤー以外の防御抑制乗数
    /// </summary>
    public const float NonPlayerDefendMultiplier = 0.3f;

    /// <summary>
    /// 小型敵への攻撃ボーナス乗数
    /// </summary>
    public const float SmallEnemyAttackMultiplier = 1.5f;

    /// <summary>
    /// 大型敵への攻撃ボーナス乗数
    /// </summary>
    public const float LargeEnemyAttackMultiplier = 1.3f;

    #endregion

    #region 戦術的判断の乗数

    /// <summary>
    /// 一撃で倒せる敵への攻撃ボーナス乗数
    /// </summary>
    public const float OneHitKillMultiplier = 3.0f;

    /// <summary>
    /// 次のターンで攻撃可能な位置への移動ボーナス乗数
    /// </summary>
    public const float NextTurnAttackPositionMultiplier = 2.0f;

    /// <summary>
    /// 2ターン後に攻撃可能な位置への移動ボーナス乗数
    /// </summary>
    public const float TwoTurnsAttackPositionMultiplier = 1.7f;

    /// <summary>
    /// HPが低い敵への移動ボーナス乗数
    /// </summary>
    public const float LowHpEnemyMoveMultiplier = 3.0f;

    #endregion
}
