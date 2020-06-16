using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace ZoomQuiz
{
	class ScoreReportBitmap:IDisposable
	{
		private const string SCORE_REPORT_FONT_NAME = "Bahnschrift Condensed";
		private readonly Size SCORE_REPORT_SIZE = new System.Drawing.Size(386, 585);
		static SizeF rowSize = new SizeF(0, 0);

		Bitmap m_bitmap;
		internal ScoreReportBitmap(List<ScoreReportEntry> scoreReport,bool times)
		{
			int fixedHeight = times ? 0 : SCORE_REPORT_SIZE.Height;
			if (rowSize.IsEmpty)
			{
				using (Bitmap testBitmap = new Bitmap(100, 100))
				{
					using (Graphics testGraphics = Graphics.FromImage(testBitmap))
					{
						testGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
						using (Font scoreReportFont = new Font(SCORE_REPORT_FONT_NAME, 20, System.Drawing.FontStyle.Bold))
						{
							rowSize = testGraphics.MeasureString("Wg", scoreReportFont);
						}
					}
				}
			}

			int xMargin = 4, yMargin = 4, ySpacing = 4;
			int rows = scoreReport.Count;
			int initialRowOffset = 9 - rows;
			if (initialRowOffset < 0 || times)
				initialRowOffset = 0;
			int currentY = yMargin + (initialRowOffset * (int)(rowSize.Height + ySpacing));
			int scoreReportHeight = ((int)(rowSize.Height + ySpacing) * (rows + 1 + initialRowOffset)) + yMargin;

			m_bitmap = new Bitmap(SCORE_REPORT_SIZE.Width, fixedHeight == 0 ? scoreReportHeight : fixedHeight);
			using (Graphics graphics = Graphics.FromImage(m_bitmap))
			{
				graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
				graphics.Clear(Color.Transparent);
				StringFormat sf = new StringFormat
				{
					Alignment = StringAlignment.Center,
					Trimming = StringTrimming.EllipsisCharacter
				};
				using (Font scoreReportFont = new Font(SCORE_REPORT_FONT_NAME, 20, System.Drawing.FontStyle.Bold))
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
									RectangleF blackRect = new RectangleF(xMargin + x, currentY + y, (SCORE_REPORT_SIZE.Width - (xMargin * 2)) + x, rowSize.Height + y);
									graphics.DrawString(sreString, scoreReportFont, Brushes.Black, blackRect, sf);
								}
						RectangleF rect = new RectangleF(xMargin, currentY, (SCORE_REPORT_SIZE.Width - (xMargin * 2)), rowSize.Height);
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
