using Godot;

public partial class Prueba : Node2D
{
    Rect2 rect = new Rect2(10, 10, 400, 200);
    bool arrastrando;
    Vector2 agarre;

    // Godot llama a este método SOLO cuando ocurre algo (clic, soltar, mover el ratón, tecla...)
    public override void _Input(InputEvent evento)
    {
        // ¿Es un evento de botón del ratón, y del botón izquierdo?
        if (evento is InputEventMouseButton boton && boton.ButtonIndex == MouseButton.Left)
        {
            if (boton.Pressed && rect.HasPoint(boton.Position))
            {
                // Acabo de pulsar dentro → lo cojo
                arrastrando = true;
                agarre = boton.Position - rect.Position;
            }
            else if (!boton.Pressed)
            {
                // Acabo de soltar → lo suelto
                arrastrando = false;
            }
        }

        // ¿Se ha movido el ratón?
        if (evento is InputEventMouseMotion movimiento && arrastrando)
        {
            rect.Position = movimiento.Position - agarre;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        bool encima = rect.HasPoint(GetGlobalMousePosition());
        Color color = (arrastrando || encima) ? Colors.Red : Colors.SteelBlue;
        DrawRect(rect, color);
    }
}
