namespace Lumos.Application;

public interface ICommand
{
    void Execute();
    void Undo();
}
