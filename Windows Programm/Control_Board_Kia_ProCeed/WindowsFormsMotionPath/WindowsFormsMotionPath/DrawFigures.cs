using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsMotionPath
{
    public static class DrawFigures
    {
        // Нарисуем стрелку в заданной точке в нормализованном направлении <nx, ny>.
        private static Graphics DrawArrowhead(this Graphics gr, Pen pen, PointF p, float nx, float ny, float length)
        {
            float ax = length * (-ny - nx);
            float ay = length * (nx - ny);
            PointF[] points = { new PointF(p.X + ax, p.Y + ay), p, new PointF(p.X - ay, p.Y + ax) };
            gr.DrawLines(pen, points);
            return gr;
        }

        private static Graphics DrawArrowback(this Graphics gr, Pen pen, PointF p, float length)
        {
            PointF[] points = { new PointF(p.X, p.Y) };
            gr.DrawEllipse(pen, points[0].X - length / 4, points[0].Y - length / 4, length / 2, length / 2);
            return gr;
        }

        // Нарисуем головки или хвосты стрелок для сегмента от p1 до p2.
        public static Graphics DrawArrow(this Graphics gr, Pen pen, PointF p1, PointF p2, float length)
        {
            // Рисуем хвост 
            gr.DrawLine(pen, p1, p2);

            // Найдите единичный вектор хвоста стрелки
            float vx = p2.X - p1.X;
            float vy = p2.Y - p1.Y;
            float dist = (float)Math.Sqrt(vx * vx + vy * vy);
            vx /= dist;
            vy /= dist;

            // Начало хвоста
            DrawArrowhead(gr, pen, p1, -vx, -vy, length);
            // Конец хвоста
            DrawArrowback(gr, new Pen(Color.Black, 2), p2, length+5);
            return gr;
        }

        public static Graphics DrawElipsesOnVector(this Graphics gr, Pen pen, PointF pStartPoint, PointF pEndPoint, float radius)
        {
            var vx = pEndPoint.X - pStartPoint.X;
            var vy = pEndPoint.Y - pStartPoint.Y;
            float distVector = (float)Math.Sqrt(vx * vx + vy * vy);
            var cosx = -vx / distVector;
            var cosy = -vy / distVector;
            float ax = radius / 2 * (cosy + cosx);
            float ay = radius / 2 * (cosy - cosx);
            gr.DrawEllipse(pen, pEndPoint.X + ax - radius / 2, pEndPoint.Y + ay - radius / 2, radius, radius);
            gr.DrawEllipse(pen, pEndPoint.X - ay - radius / 2, pEndPoint.Y + ax - radius / 2, radius, radius);
            return gr;
        }

        public static PointF[] CenteresCirclesOfVector(Vector circleVector, float radiusMidle, float _CircleDiameter)
        {
            var vx = circleVector.EndPoint.X - circleVector.StartPoint.X;
            var vy = circleVector.EndPoint.Y - circleVector.StartPoint.Y;
            float distVector = (float)Math.Sqrt(vx * vx + vy * vy);
            var cosx = -vx / distVector;
            var cosy = -vy / distVector;
            float angelx = _CircleDiameter / 2 * (cosy + cosx);
            float angely = _CircleDiameter / 2 * (cosy - cosx);

            // Центры окружностей векторов
            PointF[] points = { new PointF(circleVector.EndPoint.X + angelx - radiusMidle / 2, circleVector.EndPoint.Y + angely - radiusMidle / 2),
                                new PointF(circleVector.EndPoint.X - angely - radiusMidle / 2, circleVector.EndPoint.Y + angelx - radiusMidle / 2) };
            return points;
        }


        public static PointF[] CirclemoGetCrossPoints(PointF oCircle1, PointF oCircle2, float oCircleRadius_1, float oCircleRadius_2)
        {
            int objCount = 2;
            PointF[] obj = new PointF[objCount];
            obj[0] = new PointF();
            obj[1] = new PointF();

            var oPos1 = new PointF(0, 0);
            var oPos2 = new PointF(oCircle2.X - oCircle1.X, oCircle2.Y - oCircle1.Y);
            var c = (oCircleRadius_2 * oCircleRadius_2 - oPos2.X * oPos2.X - oPos2.Y * oPos2.Y - oCircleRadius_1 * oCircleRadius_1) / -2;
            var a = oPos2.X * oPos2.X + oPos2.Y * oPos2.Y;
            if (oPos2.X != 0)
            {
                var b = -2 * oPos2.Y * c;
                var e = c * c - oCircleRadius_1 * oCircleRadius_1 * oPos2.X * oPos2.X;
                var D = b * b - 4 * a * e;
                if (D > 0)
                {
                    obj[0] = new PointF(0, 0);
                    obj[1] = new PointF(0, 0);
                    obj[0].Y = (float)(-b + Math.Sqrt(D)) / (2 * a);
                    obj[1].Y = (float)(-b - Math.Sqrt(D)) / (2 * a);
                    obj[0].X = (c - obj[0].Y * oPos2.Y) / oPos2.X;
                    obj[1].X = (c - obj[1].Y * oPos2.Y) / oPos2.X;
                }
                else if (D == 0)
                {
                    objCount = 1;
                    obj = new PointF[objCount];
                    obj[0] = new PointF(0, 0);
                    obj[0].Y = (float)(-b + Math.Sqrt(D)) / (2 * a);
                    obj[0].X = (c - obj[0].Y * oPos2.Y) / oPos2.X;
                }
                else
                {
                    objCount = 0;
                    obj = new PointF[objCount];
                }
            }
            else
            {
                var D = oCircleRadius_1 * oCircleRadius_1 - (c * c) / (oPos2.Y * oPos2.Y);
                if (D > 0)
                {
                    obj[0] = new PointF(0, 0);
                    obj[1] = new PointF(0, 0);
                    obj[0].X = (float)Math.Sqrt(D);
                    obj[1].X = -(float)Math.Sqrt(D);
                    obj[0].Y = c / oPos2.Y;
                    obj[1].Y = c / oPos2.Y;
                }
                else if (D == 0)
                {
                    objCount = 1;
                    obj = new PointF[objCount];
                    obj[0] = new PointF(0, 0);
                    obj[0].X = 0;
                    obj[0].Y = c / oPos2.Y;
                }
                else
                {
                    objCount = 0;
                    obj = new PointF[objCount];
                }
            }

            if (obj.Length > 0)
            {
                if (obj[0] != null)
                {
                    obj[0].X += oCircle1.X;
                    obj[0].Y += oCircle1.Y;
                }

                if (obj[1] != null)
                {
                    obj[1].X += oCircle1.X;
                    obj[1].Y += oCircle1.Y;
                }
            }

            return obj;
        }

        public static Graphics StartDrawIntersectionCircles(this Graphics fig, Vector circleVector_1, Vector circleVector_2, float _CircleDiameter, bool DrawAllIntersection = true)
        {
            Pen bold_pen_1 = new Pen(Brushes.Blue, 4);
            Pen bold_pen_2 = new Pen(Brushes.Red, 4);
            Pen middle_1_pen = new Pen(Brushes.Red, 2);
            Pen middle_2_pen = new Pen(Brushes.Blue, 2);
            Pen center_Line = new Pen(Brushes.Black, 2);
            Pen bold_way_pen = new Pen(Brushes.Black, 4);

            // Центры окружностей Вектора 1
            var radiusMidle = 2;
            PointF[] points_1 = CenteresCirclesOfVector(circleVector_1, radiusMidle, _CircleDiameter);
            if (DrawAllIntersection)
            {
                fig.DrawEllipse(middle_1_pen, points_1[0].X, points_1[0].Y, radiusMidle, radiusMidle);
                fig.DrawEllipse(middle_2_pen, points_1[1].X, points_1[1].Y, radiusMidle, radiusMidle);
            }

            // Центры окружностей Вектора 2
            PointF[] points_2 = CenteresCirclesOfVector(circleVector_2, radiusMidle, _CircleDiameter);
            if (DrawAllIntersection)
            {
                fig.DrawEllipse(middle_1_pen, points_2[0].X, points_2[0].Y, radiusMidle, radiusMidle);
                fig.DrawEllipse(middle_2_pen, points_2[1].X, points_2[1].Y, radiusMidle, radiusMidle);

                // Прямая до центров 2 окружностей одного Вектора
                fig.DrawLine(middle_1_pen, points_1[0].X, points_1[0].Y, points_2[0].X, points_2[0].Y);
                fig.DrawLine(middle_2_pen, points_1[1].X, points_1[1].Y, points_2[1].X, points_2[1].Y);
            }

            // Центральная точка на прямой между 2 окружностей
            var centerLine_1 = new PointF((points_1[0].X + points_2[0].X) / 2, (points_1[0].Y + points_2[0].Y) / 2);
            var centerLine_2 = new PointF((points_1[1].X + points_2[1].X) / 2, (points_1[1].Y + points_2[1].Y) / 2);
            if (DrawAllIntersection)
            {
                fig.DrawEllipse(center_Line, centerLine_1.X, centerLine_1.Y, radiusMidle, radiusMidle);
                fig.DrawEllipse(center_Line, centerLine_2.X, centerLine_2.Y, radiusMidle, radiusMidle);
            }

            // От центральной точки прямой проводим окружность до центров окружностей векторов
            var vXLine = points_2[0].X - centerLine_1.X;
            var vYLine = points_2[0].Y - centerLine_1.Y;
            float diameterCenterLineElipse_1 = 2*(float)Math.Sqrt(vXLine * vXLine + vYLine * vYLine);
            if (DrawAllIntersection)
            {
                fig.DrawEllipse(middle_1_pen, centerLine_1.X - diameterCenterLineElipse_1 / 2, centerLine_1.Y - diameterCenterLineElipse_1 / 2, diameterCenterLineElipse_1, diameterCenterLineElipse_1);
            }

            vXLine = points_2[1].X - centerLine_2.X;
            vYLine = points_2[1].Y - centerLine_2.Y;
            float diameterCenterLineElipse_2 = 2 * (float)Math.Sqrt(vXLine * vXLine + vYLine * vYLine);
            if (DrawAllIntersection)
            {
                fig.DrawEllipse(middle_2_pen, centerLine_2.X - diameterCenterLineElipse_2 / 2, centerLine_2.Y - diameterCenterLineElipse_2 / 2, diameterCenterLineElipse_2, diameterCenterLineElipse_2);
            }

            // Находим точки пересечения окружностей Вектора 1
            var CirclemoGetCrossPoints_1_1 = CirclemoGetCrossPoints(points_1[0], centerLine_1, 50/2, diameterCenterLineElipse_1/2);
            if (DrawAllIntersection)
            {
                if (CirclemoGetCrossPoints_1_1.Length > 0)
                {
                    foreach (var point in CirclemoGetCrossPoints_1_1)
                    {
                        fig.DrawEllipse(bold_pen_1, point.X, point.Y, 3, 3);
                    }
                }
            }

            var CirclemoGetCrossPoints_1_2 = CirclemoGetCrossPoints(points_2[0], centerLine_1, 50 / 2, diameterCenterLineElipse_1 / 2);
            if (DrawAllIntersection)
            {
                if (CirclemoGetCrossPoints_1_2.Length > 0)
                {
                    foreach (var point in CirclemoGetCrossPoints_1_2)
                    {
                        fig.DrawEllipse(bold_pen_1, point.X, point.Y, 3, 3);
                    }
                }
            }

            // Находим точки пересечения окружностей Вектора 2
            var CirclemoGetCrossPoints_2_1 = CirclemoGetCrossPoints(points_1[1], centerLine_2, 50 / 2, diameterCenterLineElipse_2 / 2);
            if (DrawAllIntersection)
            {
                if (CirclemoGetCrossPoints_2_1.Length > 0)
                {
                    foreach (var point in CirclemoGetCrossPoints_2_1)
                    {
                        fig.DrawEllipse(bold_pen_2, point.X, point.Y, 3, 3);
                    }
                }
            }

            var CirclemoGetCrossPoints_2_2 = CirclemoGetCrossPoints(points_2[1], centerLine_2, 50 / 2, diameterCenterLineElipse_2 / 2);
            if (DrawAllIntersection)
            {
                if (CirclemoGetCrossPoints_2_2.Length > 0)
                {
                    foreach (var point in CirclemoGetCrossPoints_2_2)
                    {
                        fig.DrawEllipse(bold_pen_2, point.X, point.Y, 3, 3);
                    }
                }
            }

            // Рисуем траектории для окружностей Векторов
            if (DrawAllIntersection)
            {
                if (CirclemoGetCrossPoints_1_1.Length > 0 && CirclemoGetCrossPoints_1_2.Length > 0)
                {
                    fig.DrawLine(middle_1_pen, CirclemoGetCrossPoints_1_1[0], CirclemoGetCrossPoints_1_2[0]);
                }

                if (CirclemoGetCrossPoints_1_1.Length > 1 && CirclemoGetCrossPoints_1_2.Length > 1)
                {
                    fig.DrawLine(middle_1_pen, CirclemoGetCrossPoints_1_1[1], CirclemoGetCrossPoints_1_2[1]);
                }

                if (CirclemoGetCrossPoints_2_1.Length > 0 && CirclemoGetCrossPoints_2_2.Length > 0)
                {
                    fig.DrawLine(middle_2_pen, CirclemoGetCrossPoints_2_1[0], CirclemoGetCrossPoints_2_2[0]);
                }

                if (CirclemoGetCrossPoints_2_1.Length > 1 && CirclemoGetCrossPoints_2_2.Length > 1)
                {
                    fig.DrawLine(middle_2_pen, CirclemoGetCrossPoints_2_1[1], CirclemoGetCrossPoints_2_2[1]);
                }
            }
            else
            {
                var oPos2 = new PointF(circleVector_2.EndPoint.X - circleVector_1.EndPoint.X, circleVector_2.EndPoint.Y - circleVector_1.EndPoint.Y);

                if (CirclemoGetCrossPoints_1_1.Length > 1 && CirclemoGetCrossPoints_1_2.Length > 1)
                {
                    fig.DrawLine(center_Line, CirclemoGetCrossPoints_1_1[1], CirclemoGetCrossPoints_1_2[1]);
                }
                
            }

            return fig;
        }
       
    }

    public class MyLine
    {
        public double a, b, c;
    };
}
