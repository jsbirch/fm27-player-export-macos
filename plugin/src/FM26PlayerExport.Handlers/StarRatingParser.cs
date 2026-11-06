using System.Collections.Generic;

namespace FM26PlayerExport.Handlers;

public static class StarRatingParser
{
	private const string StarClass = "fm-star-rating-star";

	private const string StarNoMarginClass = "fm-star-rating-star-no-margin";

	private const string GoldFullClass = "ability-max-potential-level-fullfilled-youth-false";

	private const string GoldHalfClass = "ability-half-potential-level-none-youth-false";

	private const string SilverFullYouthClass = "ability-max-potential-level-fullfilled-youth-true";

	private const string SilverHalfClass = "ability-half-potential-level-none-youth-true";

	private const string SilverFullMinimumClass = "ability-minimum-potential-level-full-youth-false";

	private const string GoldSilverHalfClass = "ability-half-potential-level-half-youth-false";

	private const string SilverHalfPotentialClass = "ability-minimum-potential-level-half-youth-false";

	private const string EmptyClass = "ability-minimum-potential-level-none-youth-false";

	private const string EmptyYouthClass = "ability-minimum-potential-level-none-youth-true";

	public static StarVisualState Classify(IEnumerable<string> classNames)
	{
		bool flag = false;
		StarVisualState starVisualState = StarVisualState.Unknown;
		foreach (string className in classNames)
		{
			StarVisualState starVisualState2;
			char c;
			switch (className)
			{
			case "fm-star-rating-star":
			case "fm-star-rating-star-no-margin":
				flag = true;
				continue;
			default:
				switch (className.Length)
				{
				case 45:
					break;
				case 48:
					goto IL_008c;
				case 50:
					goto IL_00ad;
				case 49:
					goto IL_00e6;
				case 44:
					goto IL_00f5;
				case 47:
					goto IL_0131;
				default:
					goto IL_016d;
				}
				c = className[29];
				if (c != 'h')
				{
					if (c == 'n' && className == "ability-half-potential-level-none-youth-false")
					{
						starVisualState2 = StarVisualState.GoldHalf;
						break;
					}
				}
				else if (className == "ability-half-potential-level-half-youth-false")
				{
					starVisualState2 = StarVisualState.GoldSilverHalf;
					break;
				}
				goto IL_016d;
			case null:
				goto IL_016d;
				IL_0131:
				if (!(className == "ability-minimum-potential-level-none-youth-true"))
				{
					goto IL_016d;
				}
				starVisualState2 = StarVisualState.Empty;
				break;
				IL_00f5:
				if (!(className == "ability-half-potential-level-none-youth-true"))
				{
					goto IL_016d;
				}
				starVisualState2 = StarVisualState.SilverHalf;
				break;
				IL_016d:
				starVisualState2 = StarVisualState.NotStar;
				break;
				IL_00e6:
				if (!(className == "ability-max-potential-level-fullfilled-youth-true"))
				{
					goto IL_016d;
				}
				starVisualState2 = StarVisualState.SilverFull;
				break;
				IL_008c:
				c = className[32];
				if (c != 'f')
				{
					if (c != 'h')
					{
						if (c == 'n' && className == "ability-minimum-potential-level-none-youth-false")
						{
							starVisualState2 = StarVisualState.Empty;
							break;
						}
					}
					else if (className == "ability-minimum-potential-level-half-youth-false")
					{
						starVisualState2 = StarVisualState.SilverHalf;
						break;
					}
				}
				else if (className == "ability-minimum-potential-level-full-youth-false")
				{
					starVisualState2 = StarVisualState.SilverFull;
					break;
				}
				goto IL_016d;
				IL_00ad:
				if (!(className == "ability-max-potential-level-fullfilled-youth-false"))
				{
					goto IL_016d;
				}
				starVisualState2 = StarVisualState.GoldFull;
				break;
			}
			StarVisualState starVisualState3 = starVisualState2;
			if (starVisualState3 != 0)
			{
				if (starVisualState != StarVisualState.Unknown && starVisualState != starVisualState3)
				{
					return StarVisualState.Unknown;
				}
				starVisualState = starVisualState3;
			}
		}
		if (!flag)
		{
			return StarVisualState.NotStar;
		}
		return starVisualState;
	}

	public static bool TryParseRating(IEnumerable<IEnumerable<string>> starClassLists, out StarRatingResult result)
	{
		int num = 0;
		float num2 = 0f;
		float num3 = 0f;
		bool flag = false;
		foreach (IEnumerable<string> starClassList in starClassLists)
		{
			StarVisualState starVisualState = Classify(starClassList);
			switch (starVisualState)
			{
			case StarVisualState.Empty:
				num++;
				break;
			case StarVisualState.GoldHalf:
			case StarVisualState.GoldFull:
				if (flag)
				{
					result = StarRatingResult.Unrecognised;
					return false;
				}
				num++;
				num2 += ((starVisualState == StarVisualState.GoldFull) ? 1f : 0.5f);
				break;
			case StarVisualState.GoldSilverHalf:
				num++;
				num2 += 0.5f;
				num3 += 0.5f;
				flag = true;
				break;
			case StarVisualState.SilverHalf:
			case StarVisualState.SilverFull:
				num++;
				flag = true;
				num3 += ((starVisualState == StarVisualState.SilverFull) ? 1f : 0.5f);
				break;
			default:
				result = StarRatingResult.Unrecognised;
				return false;
			case StarVisualState.NotStar:
				break;
			}
		}
		if (num != 5)
		{
			result = StarRatingResult.Unrecognised;
			return false;
		}
		result = new StarRatingResult(num2, num3, recognised: true);
		return true;
	}
}
