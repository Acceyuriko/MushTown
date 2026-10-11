using System;
using MushTown.Client;

string[] argsList = Environment.GetCommandLineArgs();

Game1.AssetsDir = ArgValue("--assets");
Game1.ShotPath = ArgValue("--shot");

using var game = new MushTown.Client.Game1();
game.Run();

string? ArgValue(string name)
{
    int i = Array.IndexOf(argsList, name);
    return i >= 0 && i + 1 < argsList.Length ? argsList[i + 1] : null;
}
