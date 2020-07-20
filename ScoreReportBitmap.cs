using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace ZoomQuiz
{
	class ScoreReportBitmap:IDisposable
	{
		private const int SCORE_REPORT_FONT_SIZE = 24;
		static SizeF rowSize = new SizeF(0, 0);

		Bitmap m_bitmap;
		internal ScoreReportBitmap(string fontName,List<ScoreReportEntry> scoreReport,bool times,Size bitmapSize)
		{
			int fixedHeight = times ? 0 : bitmapSize.Height;
			if (rowSize.IsEmpty)
			{
				using (Bitmap testBitmap = new Bitmap(100, 100))
				{
					using (Graphics testGraphics = Graphics.FromImage(testBitmap))
					{
						testGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
						using (Font scoreReportFont = new Font(fontName, SCORE_REPORT_FONT_SIZE, FontStyle.Bold))
						{
							rowSize = testGraphics.MeasureString("Wg", scoreReportFont);
						}
					}
				}
			}

			int xMargin = 4, yMargin = 4, ySpacing = 4, rows = scoreReport.Count;
			int currentY = yMargin;
			int scoreReportHeight = ((int)(rowSize.Height + ySpacing) * (rows + 1)) + yMargin;

			m_bitmap = new Bitmap(bitmapSize.Width, fixedHeight == 0 ? scoreReportHeight : fixedHeight);
			using (Graphics graphics = Graphics.FromImage(m_bitmap))
			{
				graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
				graphics.Clear(Color.Transparent);
				StringFormat sf = new StringFormat
				{
					Alignment = StringAlignment.Center,
					Trimming = StringTrimming.EllipsisCharacter
				};
				using (Font scoreReportFont = new Font(fontName, SCORE_REPORT_FONT_SIZE, FontStyle.Bold))
				{
					List<ScoreReportEntry> workingScoreReport = new List<ScoreReportEntry>(scoreReport);
					if (times)
					{
						workingScoreReport.Sort();
						workingScoreReport.Reverse();
					}
					foreach (ScoreReportEntry sre in workingScoreReport)
					{
						string sreString = sre.GetScoreReportString(times);
						for (int x = -1; x < 2; ++x)
							for (int y = -1; y < 2; ++y)
								if (!(x == 0 && y == 0))
								{
									RectangleF blackRect = new RectangleF(xMargin + x, currentY + y, (bitmapSize.Width - (xMargin * 2)) + x, rowSize.Height + y);
									graphics.DrawString(sreString, scoreReportFont, Brushes.Black, blackRect, sf);
								}
						RectangleF rect = new RectangleF(xMargin, currentY, bitmapSize.Width - (xMargin * 2), rowSize.Height);
						graphics.DrawString(sreString, scoreReportFont, sre.Colour, rect, sf);
						currentY += (int)(rowSize.Height + ySpacing);
					}
				}
			}
		}

		internal void Save(string path)
		{
			m_bitmap.Save(path, ImageFormat.Png);
		}

		public void Dispose()
		{
			m_bitmap.Dispose();
		}
	}
}
