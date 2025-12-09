using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [UxmlElement]
    public partial class RadialProgress : VisualElement
    {
        // --- Backing Fields ---
        private float _currentValue = 0f;
        private float _maxValue = 100f; // Default to 100
        private float _lineWidth = 10f;
        private Color _trackColor = new Color(0.1f, 0.1f, 0.1f);
        private Color _fillColor = Color.green;

        // --- Exposed Properties ---

        [UxmlAttribute, CreateProperty]
        public float CurrentValue
        {
            get => _currentValue;
            set
            {
                // We don't clamp here immediately in case you want to animate
                // values that overshoot, but we check for changes to repaint.
                if (!Mathf.Approximately(_currentValue, value))
                {
                    _currentValue = value;
                    MarkDirtyRepaint();
                }
            }
        }

        [UxmlAttribute, CreateProperty]
        public float MaxValue
        {
            get => _maxValue;
            set
            {
                // Prevent value from being exactly 0 to avoid division errors later, 
                // though we also handle that in the draw logic.
                if (!Mathf.Approximately(_maxValue, value))
                {
                    _maxValue = value;
                    MarkDirtyRepaint();
                }
            }
        }

        [UxmlAttribute]
        public float LineWidth
        {
            get => _lineWidth;
            set
            {
                if (!Mathf.Approximately(_lineWidth, value))
                {
                    _lineWidth = value;
                    MarkDirtyRepaint();
                }
            }
        }

        [UxmlAttribute]
        public Color TrackColor
        {
            get => _trackColor;
            set
            {
                if (_trackColor != value)
                {
                    _trackColor = value;
                    MarkDirtyRepaint();
                }
            }
        }

        [UxmlAttribute]
        public Color FillColor
        {
            get => _fillColor;
            set
            {
                if (_fillColor != value)
                {
                    _fillColor = value;
                    MarkDirtyRepaint();
                }
            }
        }

        // --- Constructor ---
        public RadialProgress()
        {
            generateVisualContent += GenerateVisualContent;
        }
        

        // --- Drawing Logic (Painter2D) ---
        private void GenerateVisualContent(MeshGenerationContext context)
        {
            float width = contentRect.width;
            float height = contentRect.height;

            if (width < 0.01f || height < 0.01f) return;

            var painter = context.painter2D;
    
            float radius = Mathf.Min(width, height) * 0.5f - (_lineWidth * 0.5f);
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);

            // Calculate ratio (0.0 to 1.0)
            float ratio = 0f;
            if (!Mathf.Approximately(_maxValue, 0f))
            {
                ratio = _currentValue / _maxValue;
            }
            
            // Clamp ratio to ensure the circle doesn't draw more than 360 degrees
            // or backwards if values are negative.
            ratio = Mathf.Clamp01(ratio);
    
            // -90 degrees is 12 o'clock
            float startAngleDegrees = -90f; 
            // 360 degrees represents a full circle
            float sweepAngleDegrees = ratio * 360f; 
            float endAngleDegrees = startAngleDegrees + sweepAngleDegrees;

            // 1. Draw Track (Full Circle)
            painter.lineWidth = _lineWidth;
            painter.strokeColor = _trackColor;
            painter.lineCap = LineCap.Round;
    
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
            painter.Stroke();

            // 2. Draw Fill
            if (ratio > 0.001f)
            {
                painter.strokeColor = _fillColor;
                painter.BeginPath();
        
                painter.Arc(center, radius, Angle.Degrees(startAngleDegrees), Angle.Degrees(endAngleDegrees));
        
                painter.Stroke();
            }
        }   
    }
}