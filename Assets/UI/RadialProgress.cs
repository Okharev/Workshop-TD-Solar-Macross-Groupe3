using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    // [UxmlElement] automatically handles the registration in UI Builder.
    // The 'partial' keyword is required for the source generator to work.
    [UxmlElement]
    public partial class RadialProgress : VisualElement
    {
        // --- Backing Fields ---
        private float _progress = 0f;
        private float _lineWidth = 10f;
        private Color _trackColor = new Color(0.1f, 0.1f, 0.1f);
        private Color _fillColor = Color.green;

        // --- Exposed Properties ---
        // [UxmlAttribute] exposes this property to the UI Builder Inspector.
        // It automatically converts PascalCase (Progress) to kebab-case (progress) for UXML.

        [UxmlAttribute]
        public float Progress
        {
            get => _progress;
            set
            {
                float clamped = Mathf.Clamp(value, 0f, 100f);
                if (!Mathf.Approximately(_progress, clamped))
                {
                    _progress = clamped;
                    MarkDirtyRepaint(); // Tells UI Toolkit to redraw
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
            // Register the drawing callback
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
    
            // -90 degrees is 12 o'clock
            float startAngleDegrees = -90f; 
            // Calculate the length of the arc based on progress
            float sweepAngleDegrees = _progress * 3.6f; 
            // Calculate the absolute end angle
            float endAngleDegrees = startAngleDegrees + sweepAngleDegrees;

            // 1. Draw Track (Full Circle)
            painter.lineWidth = _lineWidth;
            painter.strokeColor = _trackColor;
            painter.lineCap = LineCap.Round;
    
            painter.BeginPath();
            // Draw from 0 to 360 using Angle struct for safety
            painter.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
            painter.Stroke();

            // 2. Draw Fill
            if (_progress > 0)
            {
                painter.strokeColor = _fillColor;
                painter.BeginPath();
        
                // FIX: Use Start Angle and End Angle (not Length)
                // We explicitly wrap them in Angle.Degrees() to prevent Radian confusion
                painter.Arc(center, radius, Angle.Degrees(startAngleDegrees), Angle.Degrees(endAngleDegrees));
        
                painter.Stroke();
            }
        }   
    }
}