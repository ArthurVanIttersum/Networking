
public class Board<T>
{
    private T[,] grid = new T[10, 10];

    public T SampleGrid(int row, int column)
    {
        return grid[row, column];
    }

    public void WriteToGrid(int row, int column, T value)
    {
        grid[row, column] = value;
    }

    public bool IsInBounds(int row, int column)
    {
        return row >= 0 && row < 10 && column >= 0 && column < 10;
    }
}

