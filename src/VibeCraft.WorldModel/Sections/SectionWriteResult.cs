namespace VibeCraft.WorldModel.Sections;

internal enum SectionWriteResult : byte
{
    Unchanged = 0,
    Changed = 1,
    RevisionExhausted = 2,
}
