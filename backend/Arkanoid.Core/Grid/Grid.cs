using Arkanoid.Core.Math;
namespace Arkanoid.Core.Grid;

public sealed class Grid
{
    public int Cols { get; }
    public int Rows { get; }
    public double CellSize { get; }
    public double OriginX { get; }
    public double OriginY { get; }

    public Grid(int cols, int rows, double cellSize, double originX, double originY)
    { Cols = cols; Rows = rows; CellSize = cellSize; OriginX = originX; OriginY = originY; }

    public double Width => Cols * CellSize;
    public double Height => Rows * CellSize;

    public Vec2 CellCenter(int col, int row)
        => new(OriginX + col * CellSize + CellSize / 2.0,
               OriginY + row * CellSize + CellSize / 2.0);
}
