using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace ZoomQuiz
{
	class TextImageBitmap:IDisposable
	{
		private readonly Size TEXT_SIZE = new Size(1600, 360);
		private const string QUESTION_FONT_NAME = "Impact";
		private const int TEXT_OUTLINE_THICKNESS = 5;
		readonly Bitmap m_bitmap;

		internal TextImageBitmap(string text)
		{
			string[] words = text.Split(' ');
			int textLength = text.Length;
			Brush[] availableColors = new Brush[]
			{
				Brushes.White,
				Brushes.LightGoldenrodYellow,
				Brushes.LightGray,
				Brushes.LightBlue,
				Brushes.LightCyan,
				Brushes.LightGreen,
				Brushes.LightSteelBlue,
				Brushes.LightYellow,
				Brushes.Azure,
				Brushes.AliceBlue,
				Brushes.Cornsilk,
				Brushes.FloralWhite,
				Brushes.GhostWhite,
				Brushes.Honeydew,
				Brushes.Ivory,
				Brushes.Lavender,
				Brushes.LavenderBlush,
				Brushes.LemonChiffon,
				Brushes.Linen,
				Brushes.MintCream,
				Brushes.MistyRose,
				Brushes.OldLace,
				Brushes.PapayaWhip,
				Brushes.SeaShell,
				Brushes.Snow,
				Brushes.WhiteSmoke,
				Brushes.Yellow
			};
			StringFormat sf = new StringFormat
			{
				Alignment = StringAlignment.Center
			};
			Brush textColor = availableColors[new Random().Next(0, availableColors.Length)];
			m_bitmap = new Bitmap(TEXT_SIZE.Width, TEXT_SIZE.Height);
			using (Graphics g = Graphics.FromImage(m_bitmap))
			{
				g.TextRenderingHint = TextRenderingHint.AntiAlias;
				g.Clear(Color.Transparent);
				for (int f = 10; ; f += 4)
				{
					using (Font font = new Font(QUESTION_FONT_NAME, f, FontStyle.Regular))
					{
						// We need room for the outline
						Size clientRect = new Size(TEXT_SIZE.Width - (TEXT_OUTLINE_THICKNESS * 2), TEXT_SIZE.Height - (TEXT_OUTLINE_THICKNESS * 2));
						SizeF textSize = g.MeasureString(text, font, clientRect, sf, out int charactersFitted, out int linesFitted);
						bool wordLimitReached = false;
						foreach (string word in words)
						{
							SizeF wordSize = g.MeasureString(word, font, 1000000);
							if (wordSize.Width >= clientRect.Width)
							{
								wordLimitReached = true;
								break;
							}
						}
						if ((textSize.Width >= clientRect.Width) || (textSize.Height >= clientRect.Height) || (wordLimitReached) || (charactersFitted < textLength))
						{
							using (Font realFont = new Font(QUESTION_FONT_NAME, f - 4, System.Drawing.FontStyle.Regular))
							{
								textSize = g.MeasureString(text, realFont, clientRect, sf, out charactersFitted, out linesFitted);
								int nVertOffset = (int)((TEXT_SIZE.Height - textSize.Height) / 2.0);
								Rectangle rect = new Rectangle(new Point(0, 0), TEXT_SIZE);
								rect.Offset(0, nVertOffset);
								for (int x = -TEXT_OUTLINE_THICKNESS; x <= TEXT_OUTLINE_THICKNESS; ++x)
									for (int y = -TEXT_OUTLINE_THICKNESS; y <= TEXT_OUTLINE_THICKNESS; ++y)
									{
										Rectangle borderRect = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
										borderRect.Offset(x, y);
										g.DrawString(text, realFont, Brushes.Black, borderRect, sf);
									}
								g.DrawString(text, realFont, textColor, rect, sf);
								break;
							}
						}
					}
				}
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
	}
}
