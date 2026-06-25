namespace Palmier.Application;

public interface ICommand
{
    void Execute();
    void Undo();
}
