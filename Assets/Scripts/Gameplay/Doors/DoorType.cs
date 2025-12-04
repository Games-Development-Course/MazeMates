public interface IDoor
{
    bool IsOpen();
    void TryOpen();
}

public enum DoorType
{
    Normal,
    Puzzle,
    Exit,
}
