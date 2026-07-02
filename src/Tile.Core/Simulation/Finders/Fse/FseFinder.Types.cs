namespace Tile.Core.Simulation;

public sealed partial class FseFinder
{
    private readonly struct SearchFrame
    {
        public SearchFrame(FseContext context, BehaviourKind kind)
        {
            Context = context;
            Kind = kind;
        }

        public FseContext Context { get; }

        public BehaviourKind Kind { get; }
    }

    /// <summary>
    /// 一次 F/S/E 上下文中的选择方案：FixedMask 总是全选，
    /// ExpandedMask 表示本轮至少选择一张新展开牌，SelectableMask 只用于补足消除数量。
    /// </summary>
    private readonly struct FsePick
    {
        private FsePick(ulong fixedMask, ulong expandedMask, ulong selectableMask)
        {
            FixedMask = fixedMask;
            ExpandedMask = expandedMask;
            SelectableMask = selectableMask;
        }

        public ulong FixedMask { get; }

        public ulong ExpandedMask { get; }

        public ulong SelectableMask { get; }

        public static FsePick Create(ulong fixedMask, ulong expandedMask, ulong selectableMask)
            => new(fixedMask, expandedMask, selectableMask);
    }
}
