using System.IO;

public interface ISerialize
{
    void Serialize(BinaryWriter write);
    void Deserialize(BinaryReader read);
}
