namespace Atom.Components.HierarchicalTree
{
    public interface IFullcastablePacket : ITreecastablePacket
    {
        bool allowUpcasting { get; set; }
    }
}