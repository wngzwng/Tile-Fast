using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace G42.TrajectoryCollection
{
    // 47 features in model2 sub47 column order. MUST stay identical to
    // src/g42/data/feature_subset.py SUB47_FEATURE_NAMES (ascending #num).
    // Fill fields by name; the column mapping is fixed in TrajectoryFeatures.WriteStep.
    public struct StepFeatures
    {
        public float candidate_layer;                              // col 0  (#1)
        public float candidate_x_position;                         // col 1  (#2)
        public float candidate_y_position;                         // col 2  (#3)
        public float candidate_is_x_edge;                          // col 3  (#4)
        public float candidate_is_y_edge;                          // col 4  (#5)
        public float candidate_is_corner;                          // col 5  (#6)
        public float candidate_to_board_center_l2_xy;              // col 6  (#7)
        public float candidate_support_count_below;                // col 7  (#8)
        public float candidate_side_locked_count;                  // col 8  (#9)
        public float candidate_blocked_neighbor_count;             // col 9  (#10)
        public float same_tile_in_dock_count;                      // col 10 (#12)
        public float same_tile_visible_selectable_count;           // col 11 (#13)
        public float same_tile_visible_selectable_parity;          // col 12 (#14)
        public float same_tile_visible_selectable_min_distance_xy; // col 13 (#15)
        public float same_tile_visible_selectable_min_layer_gap;   // col 14 (#17)
        public float same_tile_visible_not_selectable_count;       // col 15 (#19)
        public float same_tile_visible_not_selectable_parity;      // col 16 (#20)
        public float same_tile_visible_not_selectable_100pct;      // col 17 (#21)
        public float same_tile_visible_not_selectable_75pct;       // col 18 (#22)
        public float same_tile_visible_not_selectable_50pct;       // col 19 (#23)
        public float same_tile_visible_not_selectable_25pct;       // col 20 (#24)
        public float same_tile_vns_min_distance_xy;                // col 21 (#25)
        public float same_tile_vns_min_layer_gap;                  // col 22 (#27)
        public float same_tile_vns_min_layer;                      // col 23 (#29)
        public float same_tile_invisible_count;                    // col 24 (#31)
        public float same_tile_invisible_parity;                   // col 25 (#32)
        public float current_same_color_left;                      // col 26 (#33)
        public float dock_last_2_min_layer;                        // col 27 (#35)
        public float dock_last_2_max_layer;                        // col 28 (#36)
        public float dock_last_2_layer_difference;                 // col 29 (#38)
        public float to_left_bot_corner_distance;                  // col 30 (#47)
        public float to_right_bot_corner_distance;                 // col 31 (#48)
        public float lock_value_total_contribution;                // col 32 (#49)
        public float masked_remain_count_with_flip_tile;           // col 33 (#52)
        public float candidate_exposes_blocked_partner;            // col 34 (#54)
        public float candidate_unlock_count_after_enter;           // col 35 (#56)
        public float candidate_new_pair_count_with_optional;       // col 36 (#57)
        public float candidate_new_pair_count_internal;            // col 37 (#58)
        public float masked_visible_count_with_flip;               // col 38 (#59)
        public float dock_space_before;                            // col 39 (#63)
        public float candidate_dock_space_remain_after_enter;      // col 40 (#64)
        public float candidate_to_previous_tile_euclidean_xy;      // col 41 (#66)
        public float candidate_is_same_as_previous_enter_dock;     // col 42 (#67)
        public float candidate_is_newly_selectable;                // col 43 (#68)
        public float same_tile_at_2_steps;                         // col 44 (#71)
        public float same_tile_morethan_2_steps;                   // col 45 (#72)
        public float same_tile_in_2_steps;                         // col 46 (#74)
    }

    /// <summary>
    /// Per-puzzle trajectory feature collector. Create one instance per puzzle,
    /// feed steps via AddStepFeatures, end each trajectory with NewTrajectory,
    /// then call SaveTar to flush one tar (named by puzzleId) holding all
    /// trajectories (each a .npy of shape [steps, 47], float32, C-order).
    ///
    /// Not thread-safe by design: one instance per puzzle; parallelize across
    /// puzzles at the caller side (each instance is independent, no shared state).
    /// Output: {outputPath}/{bucket}/{puzzleId}.tar, bucket = stable hash % 1000.
    /// </summary>
    public sealed class TrajectoryFeatures
    {
        public const int FeatureCount = 47;
        private const int BucketCount = 1000;

        // Column-order mirror of feature_subset.py SUB47_FEATURE_NAMES (for error text only;
        // the real mapping is hard-coded in WriteStep). MUST stay in sync with model2.
        private static readonly string[] ColumnNames =
        {
            "candidate_layer", "candidate_x_position", "candidate_y_position",
            "candidate_is_x_edge", "candidate_is_y_edge", "candidate_is_corner",
            "candidate_to_board_center_l2_xy", "candidate_support_count_below",
            "candidate_side_locked_count", "candidate_blocked_neighbor_count",
            "same_tile_in_dock_count", "same_tile_visible_selectable_count",
            "same_tile_visible_selectable_parity", "same_tile_visible_selectable_min_distance_xy",
            "same_tile_visible_selectable_min_layer_gap", "same_tile_visible_not_selectable_count",
            "same_tile_visible_not_selectable_parity", "same_tile_visible_not_selectable_100pct",
            "same_tile_visible_not_selectable_75pct", "same_tile_visible_not_selectable_50pct",
            "same_tile_visible_not_selectable_25pct", "same_tile_vns_min_distance_xy",
            "same_tile_vns_min_layer_gap", "same_tile_vns_min_layer",
            "same_tile_invisible_count", "same_tile_invisible_parity",
            "current_same_color_left", "dock_last_2_min_layer", "dock_last_2_max_layer",
            "dock_last_2_layer_difference", "to_left_bot_corner_distance",
            "to_right_bot_corner_distance", "lock_value_total_contribution",
            "masked_remain_count_with_flip_tile", "candidate_exposes_blocked_partner",
            "candidate_unlock_count_after_enter", "candidate_new_pair_count_with_optional",
            "candidate_new_pair_count_internal", "masked_visible_count_with_flip",
            "dock_space_before", "candidate_dock_space_remain_after_enter",
            "candidate_to_previous_tile_euclidean_xy", "candidate_is_same_as_previous_enter_dock",
            "candidate_is_newly_selectable", "same_tile_at_2_steps",
            "same_tile_morethan_2_steps", "same_tile_in_2_steps",
        };

        private readonly string _puzzleId;
        private readonly string _outputPath;
        private readonly int _maxTrajectoriesPerPuzzle;
        private readonly bool _gzip;
        private readonly long _mtime;

        private readonly List<StepFeatures> _steps = new List<StepFeatures>();
        private readonly ByteBuffer _tar = new ByteBuffer(4 * 1024 * 1024);
        private readonly byte[] _tarHeaderScratch = new byte[UstarTarWriter.BlockSize];
        private readonly byte[] _npyHeaderScratch = new byte[128];
        private int _storedCount;
        private bool _saved;

        /// <param name="puzzleId">Puzzle id; becomes the tar file name (ASCII expected).</param>
        /// <param name="outputPath">Root dir; tar -> {outputPath}/{bucket}/{puzzleId}.tar.</param>
        /// <param name="maxTrajectoriesPerPuzzle">Keep only first N trajectories (default no cap).</param>
        /// <param name="gzip">If true, gzip the tar (.tar.gz). Default false (max throughput).</param>
        public TrajectoryFeatures(string puzzleId, string outputPath,
            int maxTrajectoriesPerPuzzle = int.MaxValue, bool gzip = false)
        {
            if (string.IsNullOrEmpty(puzzleId))
                throw new ArgumentException("puzzleId must be non-empty");
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("outputPath must be non-empty");
            if (maxTrajectoriesPerPuzzle < 1)
                throw new ArgumentException("maxTrajectoriesPerPuzzle must be >= 1, got " + maxTrajectoriesPerPuzzle);
            _puzzleId = puzzleId;
            _outputPath = outputPath;
            _maxTrajectoriesPerPuzzle = maxTrajectoriesPerPuzzle;
            _gzip = gzip;
            _mtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>Append one step to the current (in-progress) trajectory.</summary>
        public void AddStepFeatures(in StepFeatures step)
        {
            if (_saved)
                throw new InvalidOperationException("AddStepFeatures after SaveTar (puzzle=" + _puzzleId + ")");
            _steps.Add(step);
        }

        /// <summary>
        /// Finish the current trajectory: serialize its steps as one .npy entry into
        /// the in-memory tar buffer, then reset the step buffer. Trajectories beyond
        /// maxTrajectoriesPerPuzzle are still validated (step count) then dropped.
        /// </summary>
        public void NewTrajectory()
        {
            if (_saved)
                throw new InvalidOperationException("NewTrajectory after SaveTar (puzzle=" + _puzzleId + ")");
            int stepCount = _steps.Count;
            if (stepCount == 0)
                throw new InvalidOperationException(
                    "NewTrajectory with 0 steps (puzzle=" + _puzzleId + " traj#" + _storedCount + ")");

            if (_storedCount >= _maxTrajectoriesPerPuzzle)
            {
                _steps.Clear();
                return;
            }

            string name = _storedCount.ToString(CultureInfo.InvariantCulture) + ".npy";
            int npyHeaderLen = NpyWriter.WriteHeaderToScratch(_npyHeaderScratch, stepCount, FeatureCount);
            long npySize = (long)npyHeaderLen + (long)stepCount * FeatureCount * 4L;

            UstarTarWriter.WriteEntryHeader(_tar, _tarHeaderScratch, name, npySize, _mtime);
            _tar.WriteBytes(_npyHeaderScratch, 0, npyHeaderLen);
            for (int i = 0; i < stepCount; i++)
            {
                WriteStep(_steps[i], _storedCount, i);
            }
            UstarTarWriter.WriteEntryPadding(_tar, npySize);

            _storedCount++;
            _steps.Clear();
        }

        /// <summary>
        /// Write the tar trailer and flush this puzzle's tar to disk
        /// ({outputPath}/{bucket}/{puzzleId}.tar[.gz]) via temp file + atomic rename.
        /// Call once per puzzle, after the last NewTrajectory.
        /// </summary>
        public void SaveTar()
        {
            if (_saved)
                throw new InvalidOperationException("SaveTar called twice (puzzle=" + _puzzleId + ")");
            if (_steps.Count != 0)
                throw new InvalidOperationException(
                    "SaveTar with " + _steps.Count + " un-finalized steps; call NewTrajectory first (puzzle=" + _puzzleId + ")");
            if (_storedCount == 0)
                throw new InvalidOperationException("SaveTar with 0 stored trajectories (puzzle=" + _puzzleId + ")");

            UstarTarWriter.WriteTrailer(_tar);

            string bucket = StableBucket(_puzzleId);
            string dir = Path.Combine(_outputPath, bucket);
            Directory.CreateDirectory(dir);
            string ext = _gzip ? ".tar.gz" : ".tar";
            string finalPath = Path.Combine(dir, _puzzleId + ext);
            string tmpPath = finalPath + ".tmp";

            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
            {
                if (_gzip)
                {
                    using (var gz = new GZipStream(fs, CompressionLevel.Fastest))
                    {
                        gz.Write(_tar.RawArray, 0, _tar.Length);
                    }
                }
                else
                {
                    fs.Write(_tar.RawArray, 0, _tar.Length);
                }
            }

            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            File.Move(tmpPath, finalPath);
            _saved = true;
        }

        // Maps the 47 struct fields to row columns 0..46. THIS ORDER IS THE CONTRACT:
        // it MUST equal feature_subset.py SUB47_FEATURE_NAMES (ascending #num). Do not reorder.
        private void WriteStep(in StepFeatures s, int trajIdx, int stepIdx)
        {
            WriteFeature(s.candidate_layer, 0, trajIdx, stepIdx);
            WriteFeature(s.candidate_x_position, 1, trajIdx, stepIdx);
            WriteFeature(s.candidate_y_position, 2, trajIdx, stepIdx);
            WriteFeature(s.candidate_is_x_edge, 3, trajIdx, stepIdx);
            WriteFeature(s.candidate_is_y_edge, 4, trajIdx, stepIdx);
            WriteFeature(s.candidate_is_corner, 5, trajIdx, stepIdx);
            WriteFeature(s.candidate_to_board_center_l2_xy, 6, trajIdx, stepIdx);
            WriteFeature(s.candidate_support_count_below, 7, trajIdx, stepIdx);
            WriteFeature(s.candidate_side_locked_count, 8, trajIdx, stepIdx);
            WriteFeature(s.candidate_blocked_neighbor_count, 9, trajIdx, stepIdx);
            WriteFeature(s.same_tile_in_dock_count, 10, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_selectable_count, 11, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_selectable_parity, 12, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_selectable_min_distance_xy, 13, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_selectable_min_layer_gap, 14, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_not_selectable_count, 15, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_not_selectable_parity, 16, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_not_selectable_100pct, 17, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_not_selectable_75pct, 18, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_not_selectable_50pct, 19, trajIdx, stepIdx);
            WriteFeature(s.same_tile_visible_not_selectable_25pct, 20, trajIdx, stepIdx);
            WriteFeature(s.same_tile_vns_min_distance_xy, 21, trajIdx, stepIdx);
            WriteFeature(s.same_tile_vns_min_layer_gap, 22, trajIdx, stepIdx);
            WriteFeature(s.same_tile_vns_min_layer, 23, trajIdx, stepIdx);
            WriteFeature(s.same_tile_invisible_count, 24, trajIdx, stepIdx);
            WriteFeature(s.same_tile_invisible_parity, 25, trajIdx, stepIdx);
            WriteFeature(s.current_same_color_left, 26, trajIdx, stepIdx);
            WriteFeature(s.dock_last_2_min_layer, 27, trajIdx, stepIdx);
            WriteFeature(s.dock_last_2_max_layer, 28, trajIdx, stepIdx);
            WriteFeature(s.dock_last_2_layer_difference, 29, trajIdx, stepIdx);
            WriteFeature(s.to_left_bot_corner_distance, 30, trajIdx, stepIdx);
            WriteFeature(s.to_right_bot_corner_distance, 31, trajIdx, stepIdx);
            WriteFeature(s.lock_value_total_contribution, 32, trajIdx, stepIdx);
            WriteFeature(s.masked_remain_count_with_flip_tile, 33, trajIdx, stepIdx);
            WriteFeature(s.candidate_exposes_blocked_partner, 34, trajIdx, stepIdx);
            WriteFeature(s.candidate_unlock_count_after_enter, 35, trajIdx, stepIdx);
            WriteFeature(s.candidate_new_pair_count_with_optional, 36, trajIdx, stepIdx);
            WriteFeature(s.candidate_new_pair_count_internal, 37, trajIdx, stepIdx);
            WriteFeature(s.masked_visible_count_with_flip, 38, trajIdx, stepIdx);
            WriteFeature(s.dock_space_before, 39, trajIdx, stepIdx);
            WriteFeature(s.candidate_dock_space_remain_after_enter, 40, trajIdx, stepIdx);
            WriteFeature(s.candidate_to_previous_tile_euclidean_xy, 41, trajIdx, stepIdx);
            WriteFeature(s.candidate_is_same_as_previous_enter_dock, 42, trajIdx, stepIdx);
            WriteFeature(s.candidate_is_newly_selectable, 43, trajIdx, stepIdx);
            WriteFeature(s.same_tile_at_2_steps, 44, trajIdx, stepIdx);
            WriteFeature(s.same_tile_morethan_2_steps, 45, trajIdx, stepIdx);
            WriteFeature(s.same_tile_in_2_steps, 46, trajIdx, stepIdx);
        }

        private void WriteFeature(float v, int col, int trajIdx, int stepIdx)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                throw new InvalidOperationException(
                    "Non-finite feature (puzzle=" + _puzzleId + " traj#" + trajIdx + " step#" + stepIdx
                    + " col#" + col + " " + ColumnNames[col] + " value="
                    + v.ToString(CultureInfo.InvariantCulture) + ")");
            }
            _tar.WriteFloatLittleEndian(v);
        }

        // Stable FNV-1a hash of puzzleId -> [0, BucketCount). MUST be deterministic across
        // processes/runs: never use string.GetHashCode (randomized per process).
        // Assumes ASCII puzzleId (uses low byte of each char); non-ASCII ids are not
        // guaranteed to bucket consistently across languages.
        private static string StableBucket(string puzzleId)
        {
            uint h = 2166136261u;
            for (int i = 0; i < puzzleId.Length; i++)
            {
                h ^= (byte)puzzleId[i];
                h *= 16777619u;
            }
            int bucket = (int)(h % BucketCount);
            return bucket.ToString("D3", CultureInfo.InvariantCulture);
        }
    }
}
