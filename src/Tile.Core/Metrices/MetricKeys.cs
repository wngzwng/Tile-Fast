namespace Tile.Core.Metrices;

public static class MetricKeys
{
    #region int

    public static readonly MetricKey<int> SuccessCount =
        new("success_count");

    public static readonly MetricKey<int> FailureCount =
        new("failure_count");

    public static readonly MetricKey<int> MoveCount =
        new("move_count");

    public static readonly MetricKey<int> CandidateCount =
        new("candidate_count");

    public static readonly MetricKey<int> NoCandidateCount =
        new("no_candidate_count");

    public static readonly MetricKey<int> MaxCandidateCount =
        new("max_candidate_count");

    #endregion

    #region double

    public static readonly MetricKey<double> FailRate =
        new("fail_rate");

    public static readonly MetricKey<double> SuccessRate =
        new("success_rate");

    public static readonly MetricKey<double> AvgScore =
        new("avg_score");

    public static readonly MetricKey<double> AvgMoveCount =
        new("avg_move_count");

    public static readonly MetricKey<double> AvgCandidateCount =
        new("avg_candidate_count");

    public static readonly MetricKey<double> AvgSelectedScore =
        new("avg_selected_score");

    public static readonly MetricKey<double> ElapsedMs =
        new("elapsed_ms");

    #endregion

    #region bool

    public static readonly MetricKey<bool> IsSuccess =
        new("is_success");

    public static readonly MetricKey<bool> HasNoCandidate =
        new("has_no_candidate");

    public static readonly MetricKey<bool> HasDeadEnd =
        new("has_dead_end");

    public static readonly MetricKey<bool> IsTimeout =
        new("is_timeout");

    public static readonly MetricKey<bool> IsValidLevel =
        new("is_valid_level");

    public static readonly MetricKey<bool> HasParseError =
        new("has_parse_error");

    #endregion

    #region string

    public static readonly MetricKey<string> LevelId =
        new("level_id");

    public static readonly MetricKey<string> FinderType =
        new("finder_type");

    public static readonly MetricKey<string> ScorerType =
        new("scorer_type");

    public static readonly MetricKey<string> StrategyType =
        new("strategy_type");

    public static readonly MetricKey<string> FailReason =
        new("fail_reason");

    public static readonly MetricKey<string> ProfileName =
        new("profile_name");

    public static readonly MetricKey<string> DebugLabel =
        new("debug_label");

    public static readonly MetricKey<string> RuleName =
        new("rule_name");

    public static readonly MetricKey<string> InputFile =
        new("input_file");

    #endregion

    #region List<int>

    public static readonly MetricKey<List<int>> CandidateCountList =
        new("candidate_count_list");

    public static readonly MetricKey<List<int>> PrunedCandidateCountList =
        new("pruned_candidate_count_list");

    public static readonly MetricKey<List<int>> MoveCountPerRun =
        new("move_count_per_run");

    public static readonly MetricKey<List<int>> SlotUsageAfterMoveList =
        new("slot_usage_after_move_list");

    public static readonly MetricKey<List<int>> ClearStepList =
        new("clear_step_list");

    #endregion

    #region List<double>

    public static readonly MetricKey<List<double>> CandidateScoreList =
        new("candidate_score_list");

    public static readonly MetricKey<List<double>> SelectedScoreList =
        new("selected_score_list");

    public static readonly MetricKey<List<double>> ElapsedMsList =
        new("elapsed_ms_list");

    public static readonly MetricKey<List<double>> SoftmaxProbabilityList =
        new("softmax_probability_list");

    public static readonly MetricKey<List<double>> ScoreDeltaList =
        new("score_delta_list");

    #endregion
}
