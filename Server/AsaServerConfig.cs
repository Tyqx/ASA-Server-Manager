using System;

namespace ASAServerManager.Server
{
    public class AsaServerConfig
    {
        public string ServerName { get; set; } =
            "ASA Server";

        public string Map { get; set; } =
            "TheIsland_WP";

        public int ServerPort { get; set; } =
            7777;

        public int QueryPort { get; set; } =
            27015;

        public int MaxPlayers { get; set; } =
            70;

        public string ServerPassword { get; set; } =
            "";

        public string AdminPassword { get; set; } =
            "";

        public string Difficulty { get; set; } =
            "1.0";

        public bool PvE { get; set; } =
            false;

        public bool Crossplay { get; set; } =
            true;

        public string ExtraArguments { get; set; } =
            "";
    }
}