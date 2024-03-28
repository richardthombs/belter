public class QuadTreeNode
{
    public Rectangle Bounds { get; init; } = new Rectangle ( 0, 0, 0, 0 );
    public QuadTreeChildren? Children { get; private set; } = null;
    public List<GameObject> Objects { get; private set; } = [];
    public int Capacity { get; init; } = 1000;

    public bool Add(GameObject obj)
    {
        if (!Bounds.Contains(obj)) return false;

        if (Children != null)
        {
            return
                Children.TopLeft!.Add(obj) ||
                Children.TopRight!.Add(obj) ||
                Children.BottomLeft!.Add(obj) ||
                Children.BottomRight!.Add(obj);
        }

        Objects.Add(obj);

        if (Objects.Count >= Capacity)
        {
            Quadsect();
        }

        return true;
    }

    public List<GameObject> FindWithin(Rectangle rect)
    {
        if (!Bounds.Intersects(rect)) return [];

        List<GameObject> found = [];

        foreach (var obj in Objects)
        {
            if (rect.Contains(obj)) found.Add(obj);
        }

        if (Children?.TopLeft != null) found.AddRange(Children.TopLeft.FindWithin(rect));
        if (Children?.TopRight != null) found.AddRange(Children.TopRight.FindWithin(rect));
        if (Children?.BottomLeft != null) found.AddRange(Children.BottomLeft.FindWithin(rect));
        if (Children?.BottomRight != null) found.AddRange(Children.BottomRight.FindWithin(rect));

        return found;
    }

    public void Quadsect()
    {
        Children = new QuadTreeChildren
        {
            TopLeft = new QuadTreeNode { Bounds = Bounds.TopLeftQuadrant, Capacity = Capacity },
            TopRight = new QuadTreeNode { Bounds = Bounds.TopRightQuadrant, Capacity = Capacity },
            BottomLeft = new QuadTreeNode { Bounds = Bounds.BottomLeftQuandrant, Capacity = Capacity },
            BottomRight = new QuadTreeNode { Bounds = Bounds.BottomRightQuadrant, Capacity = Capacity }
        };

        if (false)
        {
            Console.WriteLine($"Quadsecting {Bounds}");
            Console.WriteLine($"TL = {Children.TopLeft.Bounds}");
            Console.WriteLine($"TR = {Children.TopRight.Bounds}");
            Console.WriteLine($"BL = {Children.BottomLeft.Bounds}");
            Console.WriteLine($"BR = {Children.BottomRight.Bounds}");
        }

        var objectsToReassign = Objects;
        Objects = [];

        foreach (var o in objectsToReassign)
        {
            if (!Add(o))
            {
                throw new ApplicationException($"Couldn't find a suitable child node: {Bounds}, obj({o.X}, {o.Y})");
            }
        }
    }
}

public class QuadTreeChildren
{
    public QuadTreeNode? TopLeft { get; init; }
    public QuadTreeNode? TopRight { get; init; }
    public QuadTreeNode? BottomLeft { get; init; }
    public QuadTreeNode? BottomRight { get; init; }
}
