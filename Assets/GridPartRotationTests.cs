using UnityEngine;
using NUnit.Framework;

public class GridPartRotationTests
{
    [Test]
    public void TestPartDimensions()
    {
        // Test a 3x1 part
        var part = new GridPart(3, 1, Vector2Int.zero, PartRotation.Degrees0);
        
        // At 0°: ActualWidth=3, ActualHeight=1
        Assert.AreEqual(3, part.ActualWidth);
        Assert.AreEqual(1, part.ActualHeight);
        Assert.IsFalse(part.IsVerticalRotation());
        
        // At 90°: ActualWidth=1, ActualHeight=3 (dimensions swap)
        part.SetRotation(PartRotation.Degrees90);
        Assert.AreEqual(1, part.ActualWidth);
        Assert.AreEqual(3, part.ActualHeight);
        Assert.IsTrue(part.IsVerticalRotation());
        
        // At 180°: ActualWidth=3, ActualHeight=1 (back to original)
        part.SetRotation(PartRotation.Degrees180);
        Assert.AreEqual(3, part.ActualWidth);
        Assert.AreEqual(1, part.ActualHeight);
        Assert.IsFalse(part.IsVerticalRotation());
        
        // At 270°: ActualWidth=1, ActualHeight=3 (dimensions swap again)
        part.SetRotation(PartRotation.Degrees270);
        Assert.AreEqual(1, part.ActualWidth);
        Assert.AreEqual(3, part.ActualHeight);
        Assert.IsTrue(part.IsVerticalRotation());
    }
    
    [Test]
    public void TestVisualCenter()
    {
        // Test 2x3 part at position (5,5)
        var part = new GridPart(2, 3, new Vector2Int(5, 5), PartRotation.Degrees0);
        
        // At 0°: center should be at (5.5, 6) - middle of 2x3 area
        Vector2 center = part.GetVisualCenter();
        Assert.AreEqual(5.5f, center.x, 0.01f);
        Assert.AreEqual(6.0f, center.y, 0.01f);
        
        // At 90°: dimensions become 3x2, center should be at (6, 5.5)
        part.SetRotation(PartRotation.Degrees90);
        center = part.GetVisualCenter();
        Assert.AreEqual(6.0f, center.x, 0.01f);
        Assert.AreEqual(5.5f, center.y, 0.01f);
    }
    
    [Test]
    public void TestOccupiedCells()
    {
        // Test 2x1 part at position (3,3)
        var part = new GridPart(2, 1, new Vector2Int(3, 3), PartRotation.Degrees0);
        
        // At 0°: should occupy (3,3) and (4,3)
        var cells = part.GetOccupiedCells();
        Assert.AreEqual(2, cells.Length);
        Assert.Contains(new Vector2Int(3, 3), cells);
        Assert.Contains(new Vector2Int(4, 3), cells);
        
        // At 90°: should occupy (3,3) and (3,4)
        part.SetRotation(PartRotation.Degrees90);
        cells = part.GetOccupiedCells();
        Assert.AreEqual(2, cells.Length);
        Assert.Contains(new Vector2Int(3, 3), cells);
        Assert.Contains(new Vector2Int(3, 4), cells);
    }
    
    [Test]
    public void TestBoundaryValidation()
    {
        // Test placing a 3x1 part near grid boundary
        var part = new GridPart(3, 1, Vector2Int.zero, PartRotation.Degrees0);
        
        // Should fit at (0,0) in a 30x53 grid
        Assert.IsTrue(part.CanBePlacedAt(Vector2Int.zero, 30, 53));
        
        // Should not fit at (28,0) - would extend to (30,0) which is out of bounds
        Assert.IsFalse(part.CanBePlacedAt(new Vector2Int(28, 0), 30, 53));
        
        // When rotated 90°, it becomes 1x3, so should fit at (28,0)
        part.SetRotation(PartRotation.Degrees90);
        Assert.IsTrue(part.CanBePlacedAt(new Vector2Int(28, 0), 30, 53));
        
        // But should not fit at (28,51) - would extend to (28,53) which is out of bounds
        Assert.IsFalse(part.CanBePlacedAt(new Vector2Int(28, 51), 30, 53));
    }
    
    [Test]
    public void TestLocalToGridTransform()
    {
        // Test 2x3 part at position (10,10)
        var part = new GridPart(2, 3, new Vector2Int(10, 10), PartRotation.Degrees0);
        
        // At 0°: local (0,0) should map to grid (10,10)
        Vector2Int worldPos = part.TransformLocalToGrid(Vector2Int.zero);
        Assert.AreEqual(new Vector2Int(10, 10), worldPos);
        
        // At 0°: local (1,2) should map to grid (11,12)
        worldPos = part.TransformLocalToGrid(new Vector2Int(1, 2));
        Assert.AreEqual(new Vector2Int(11, 12), worldPos);
        
        // At 90°: local (0,0) should map to grid (12,9) (rotated around part center)
        part.SetRotation(PartRotation.Degrees90);
        worldPos = part.TransformLocalToGrid(Vector2Int.zero);
        Assert.AreEqual(new Vector2Int(12, 9), worldPos);
    }
}