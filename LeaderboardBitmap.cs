using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace ZoomQuiz
{
	class LeaderboardBitmap:IDisposable
	{
		private readonly System.Drawing.Size LEADERBOARD_SIZE = new Size(1860, 1000);
		private const string LEADERBOARD_FONT_NAME = "Bahnschrift Condensed";

		Bitmap m_bitmap;

		private void DrawScore(Graphics g, Rectangle r, ContestantScore score, bool odd)
		{
			int textOffset = 25;
			g.FillRectangle(odd ? Brushes.WhiteSmoke : Brushes.GhostWhite, r);
			Rectangle posRect = new Rectangle(r.Left, r.Top, r.Height, r.Height);
			Rectangle nameRect = new Rectangle(r.Left + r.Height, r.Top + textOffset, r.Width - (r.Height * 2), r.Height - (textOffset * 2));
			Rectangle scoreRect = new Rectangle((r.Left + r.Width) - r.Height, r.Top, r.Height, r.Height);
			g.FillRectangle(odd ? Brushes.Honeydew : Brushes.Azure, posRect);
			g.FillRectangle(odd ? Brushes.Lavender : Brushes.LavenderBlush, scoreRect);
			g.DrawLine(Pens.Black, r.Left, r.Top, r.Left, r.Bottom);
			g.DrawLine(Pens.Black, r.Right, r.Top, r.Right, r.Bottom);
			posRect.Offset(0, textOffset);
			nameRect.Offset(12, 0);
			scoreRect.Offset(0, textOffset);
			if (score != null)
				using (Font leaderboardFont = new Font(LEADERBOARD_FONT_NAME, 36, FontStyle.Bold))
				{
					StringFormat sf = new StringFormat
					{
						Trimming = StringTrimming.EllipsisCharacter
					};
					g.DrawString(score.Name, leaderboardFont, Brushes.Black, nameRect, sf);
					sf.Alignment = StringAlignment.Center;
					sf.Trimming = StringTrimming.None;
					g.DrawString(score.PositionString, leaderboardFont, Brushes.Black, posRect, sf);
					g.DrawString("" + score.Score, leaderboardFont, Brushes.Black, scoreRect, sf);
				}
		}

		public void Dispose()
		{
			m_bitmap.Dispose();
		}

		internal void Save(string path)
		{
			m_bitmap.Save(path, ImageFormat.Png);
		}

		internal LeaderboardBitmap(List<ContestantScore> scores,int leaderboardIndex,ref int scoreIndex)
		{
			StringFormat sf = new StringFormat
			{
				Alignment = StringAlignment.Center
			};
			m_bitmap = new Bitmap(LEADERBOARD_SIZE.Width,LEADERBOARD_SIZE.Height);
			using (Graphics g = Graphics.FromImage(m_bitmap))
			{
				g.TextRenderingHint = TextRenderingHint.AntiAlias;
				g.Clear(Color.Transparent);
				Rectangle headerRect = new Rectangle(0, 0, LEADERBOARD_SIZE.Width, 100);
				using (Font leaderboardHeaderFont = new Font(LEADERBOARD_FONT_NAME, 40, FontStyle.Bold))
				{
					g.FillRectangle(Brushes.PapayaWhip, headerRect);
					g.DrawRectangle(Pens.Black, headerRect.Left, headerRect.Top, headerRect.Width - 1, headerRect.Height - 1);
					headerRect.Offset(0, 20);
					g.DrawString("Leaderboard (page " + leaderboardIndex + ")", leaderboardHeaderFont, Brushes.Navy, headerRect, sf);
				}
				// Leaves 900 pixels.
				for (int x = 0; x < 3; ++x)
					for (int y = 0; y < 9; ++y)
						DrawScore(g, new Rectangle(x * 620, 100 + (y * 100), 620, 100), scoreIndex < scores.Count ? scores[scoreIndex++] : null, y % 2 == 1);
				g.DrawRectangle(Pens.Black, 0, 0, LEADERBOARD_SIZE.Width - 1, LEADERBOARD_SIZE.Height - 1);
			}
		}
	}
}
