using System.Collections.Generic;

// Rules are independent of animation and input. A full board still ends the round.
public sealed class ThrowBoard
{
    public enum Move { Invalid, OwnSquare, Claimed, Captured }
    public readonly int[] Cells = new int[9];
    public int Filled { get; private set; }
    public int Player { get; private set; }
    public bool Finished { get; private set; }
    public bool Draw { get; private set; }
    public readonly List<int> WinningCells = new List<int>();
    static readonly int[,] Lines = { {0,1,2}, {3,4,5}, {6,7,8}, {0,3,6}, {1,4,7}, {2,5,8}, {0,4,8}, {2,4,6} };

    public ThrowBoard() { Reset(); }
    public void Reset()
    {
        for (int i = 0; i < 9; i++) Cells[i] = -1;
        Filled = 0; Player = 1; Finished = Draw = false; WinningCells.Clear();
    }
    public Move Apply(int cell)
    {
        if (Finished || cell < 0 || cell >= 9) return Move.Invalid;
        if (Cells[cell] == Player) return Move.OwnSquare;
        var move = Cells[cell] < 0 ? Move.Claimed : Move.Captured;
        if (move == Move.Claimed) Filled++;
        Cells[cell] = Player;
        Finished = FindWin();
        if (!Finished && Filled == 9) Finished = Draw = true;
        // Keep the winner selected; otherwise the next player takes their turn.
        if (!Finished) Player = 1 - Player;
        return move;
    }
    public bool FindWin()
    {
        WinningCells.Clear();
        for (int i = 0; i < 8; i++)
        {
            int a = Lines[i,0], b = Lines[i,1], c = Lines[i,2];
            if (Cells[a] >= 0 && Cells[a] == Cells[b] && Cells[a] == Cells[c])
            { WinningCells.Add(a); WinningCells.Add(b); WinningCells.Add(c); return true; }
        }
        return false;
    }
}
