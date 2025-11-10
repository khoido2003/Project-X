using System.Collections.Generic;

public static class RoomNameService
{
    private static readonly HashSet<int> usedIndexes = new();

    public static string GetNextRoomName()
    {
        int i = 1;
        while (usedIndexes.Contains(i))
        {
            i++;
        }
        usedIndexes.Add(i);
        return $"Room {i}";
    }

    public static void FreeRoomName(string roomName)
    {
        if (roomName.StartsWith("Room "))
        {
            if (int.TryParse(roomName.Substring(5), out int idx))
            {
                usedIndexes.Remove(idx);
            }
        }
    }
}
