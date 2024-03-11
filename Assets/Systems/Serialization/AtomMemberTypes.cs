namespace Atom.Serialization
{
    public enum AtomMemberTypes
    {
        Byte, // 1
        SByte, // 1
        Short, // 2
        UShort, // 2
        Int, // 4
        UInt, // 4
        Long, // 8
        ULong, // 8
        Float, // 4
        Double, // 8
        Bool, // 1
        Char, // 2
        String, // 4b ++
        Decimal, // 24
        DateTime, // 8
        DateSpan,
        Enum, // 4 ?
        Object, // dyn
    }
}
