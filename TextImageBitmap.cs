using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace ZoomQuiz
{
	class TextImageBitmap:IDisposable
	{
		private const float FONT_SIZE_CALCULATION_PRECISION = 2.0f;
		private const int TEXT_OUTLINE_THICKNESS = 5;
		readonly Bitmap m_bitmap;

		internal TextImageBitmap(string text,string fontName,Size textGraphicSize)
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
			m_bitmap = new Bitmap(textGraphicSize.Width, textGraphicSize.Height);
			using (Graphics g = Graphics.FromImage(m_bitmap))
			{
				g.TextRenderingHint = TextRenderingHint.AntiAlias;
				g.Clear(Color.Transparent);
				if (!string.IsNullOrEmpty(text))
				{
					float fontSizeDiff = 32.0f;
					int measurements = 0;
					for (float f = 50; ; f += fontSizeDiff)
					{
						using (Font font = new Font(fontName, f, FontStyle.Regular))
						{
							// We need room for the outline
							Size clientRect = new Size(textGraphicSize.Width - (TEXT_OUTLINE_THICKNESS * 2), textGraphicSize.Height - (TEXT_OUTLINE_THICKNESS * 2));
							SizeF textSize = g.MeasureString(text, font, clientRect, sf, out int charactersFitted, out int linesFitted);
							++measurements;
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
							// If we exceed the size, we need to start reducing, unless the fontSizeDiff is already as small as it can get
							if ((textSize.Width >= clientRect.Width) || (textSize.Height >= clientRect.Height) || wordLimitReached || (charactersFitted < textLength))
							{
								if (fontSizeDiff > 0.0 && fontSizeDiff <= FONT_SIZE_CALCULATION_PRECISION)
									using (Font realFont = new Font(fontName, f - FONT_SIZE_CALCULATION_PRECISION, FontStyle.Regular))
									{
										//System.Windows.MessageBox.Show("" + measurements);
										textSize = g.MeasureString(text, realFont, clientRect, sf, out charactersFitted, out linesFitted);
										int nVertOffset = (int)((textGraphicSize.Height - textSize.Height) / 2.0);
										Rectangle rect = new Rectangle(new Point(0, 0), textGraphicSize);
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
								else if (fontSizeDiff > 0.0f)
									fontSizeDiff = -fontSizeDiff / 2.0f;
							}
							else if (fontSizeDiff < 0.0f)
								fontSizeDiff = -fontSizeDiff / 2.0f;
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
