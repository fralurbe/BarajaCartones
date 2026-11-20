using Godot;

public partial class Prueba : Node2D
{
    // Estado que sobrevive entre frames
    Rect2 rect = new Rect2(10, 10, 400, 200);
    bool arrastrando;
    bool pulsadoAntes;
    Vector2 agarre;

    public override void _Process(double delta)
    {
        Vector2 raton = GetGlobalMousePosition();
        bool pulsadoAhora = Input.IsMouseButtonPressed(MouseButton.Left);

        // 1) Acabo de pulsar (ahora sí, antes no) y estoy dentro → lo cojo
        if (pulsadoAhora && !pulsadoAntes && rect.HasPoint(raton))
        {
            arrastrando = true;
            agarre = raton - rect.Position;   // dónde lo he cogido, relativo a la esquina
        }

        // 2) He soltado el botón → lo suelto
        if (!pulsadoAhora)
            arrastrando = false;

        // 3) Mientras lo arrastro, la esquina va al ratón menos el agarre
        if (arrastrando)
            rect.Position = raton - agarre;

        pulsadoAntes = pulsadoAhora;
        QueueRedraw();
    }

    public override void _Draw()
    {
        bool encima = rect.HasPoint(GetGlobalMousePosition());
        Color color = (arrastrando || encima) ? Colors.Red : Colors.SteelBlue;
        DrawRect(rect, color);
    }
}
