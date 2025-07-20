# Part Rotation System - Solution Documentation

## Problem Statement
Parts with unequal width (W) and height (H) appeared visually incorrect when rotated, and their associated splines misaligned with the visual output.

## Solution Overview
Implemented a comprehensive part rotation system that correctly handles parts with W != H dimensions:

### Key Components

#### 1. GridPart Class (`GridPart.cs`)
- **Dimension Management**: Properly swaps `gridWidth` and `gridHeight` for 90°/270° rotations
- **Pivot Calculation**: Calculates visual center based on actual dimensions after rotation
- **Transformation**: Provides accurate local-to-grid coordinate transformation
- **Validation**: Ensures parts fit within grid boundaries after rotation

#### 2. PartVisualizer Class (`PartVisualizer.cs`)
- **Visual Rendering**: Creates 3D representations of parts with correct scaling
- **Rotation Indicators**: Shows rotation state with directional arrows
- **Material Distinction**: Uses different colors for W != H parts (purple) vs W == H parts (green)
- **Gizmo Drawing**: Provides editor visualizations

#### 3. Enhanced Grid Editor (`GridTrackEditorWindow.cs`)
- **Part Edit Mode**: Interactive placement and rotation of parts
- **Visual Feedback**: Real-time display of part dimensions and rotation
- **Mouse Controls**: Left-click to place, right-click to rotate/delete
- **Dimension UI**: Controls for setting part width, height, and rotation

#### 4. Updated Level Visualizer (`LevelVisualizer.cs`)
- **Spline Alignment**: Ensures splines align correctly with rotated parts
- **Integration**: Seamlessly works with the existing track system

## Technical Solution Details

### Dimension Swapping
```csharp
public int ActualWidth => IsVerticalRotation() ? gridHeight : gridWidth;
public int ActualHeight => IsVerticalRotation() ? gridWidth : gridHeight;
```

### Correct Pivot Calculation
```csharp
public Vector2 GetVisualCenter()
{
    float centerX = position.x + (ActualWidth - 1) * 0.5f;
    float centerY = position.y + (ActualHeight - 1) * 0.5f;
    return new Vector2(centerX, centerY);
}
```

### Rotation Transform
The system properly transforms local coordinates to grid coordinates based on rotation:
- **0°**: No transformation
- **90°**: `(x,y) -> (y, width-1-x)`
- **180°**: `(x,y) -> (width-1-x, height-1-y)`
- **270°**: `(x,y) -> (height-1-y, x)`

## Test Validation
The solution includes comprehensive tests (`GridPartRotationTests.cs`) that verify:
- Dimension swapping works correctly
- Pivot points are calculated accurately
- Boundary validation prevents invalid placements
- Coordinate transformations are precise

## Usage Examples

### Creating a Rotated Part
```csharp
var part = new GridPart(4, 2, new Vector2Int(10, 10), PartRotation.Degrees90);
// Creates a 4x2 part that appears as 2x4 when rotated 90°
```

### Using in Grid Editor
1. Open Grid Track Editor window (Tools > Grid Track Editor)
2. Click "Part Edit Mode"
3. Set desired Width and Height
4. Choose rotation (0°, 90°, 180°, 270°)
5. Left-click to place parts
6. Right-click on existing parts to rotate or delete

### Visual Indicators
- **Purple parts**: W != H (unequal dimensions)
- **Green parts**: W == H (equal dimensions)
- **Red arrows**: Show current rotation direction
- **Part labels**: Display dimensions and rotation angle

## Testing Data
- `test_rotation.json`: Basic rotation test cases
- `demo_rotated_parts.json`: Comprehensive demonstration with various part sizes and rotations

## Problem Resolution Checklist
✅ Parts with W != H render correctly after rotation (90°, 180°, 270°) without visual distortion
✅ Splines align perfectly with the rotated parts
✅ Pivot points are calculated correctly as the actual center of parts
✅ Dimension swapping works properly for 90°/270° rotations
✅ Grid editor provides intuitive part placement and rotation controls
✅ Visual indicators clearly show rotation state and part boundaries

The solution ensures that both visual representation and spline alignment work correctly for parts of any dimensions at any rotation angle.