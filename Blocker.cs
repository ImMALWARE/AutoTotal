using System.IO;

namespace AutoTotal
{
    internal static class Blocker
    {

        public static void Block(string path)
        {
            File.WriteAllText(path + ":Zone.Identifier:$DATA", "[ZoneTransfer]\nZoneId=4");
        }

        public static void Unblock(string path)
        {
            File.Delete(path + ":Zone.Identifier:$DATA");
        }
    }
}
