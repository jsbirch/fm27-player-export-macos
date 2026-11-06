namespace FM26PlayerExport.Handlers;

public sealed class StarRatingResult
{
	public static readonly StarRatingResult Unrecognised = new StarRatingResult(0f, 0f, recognised: false);

	public float GoldStars { get; }

	public float SilverStars { get; }

	public float DisplayedStars => GoldStars + SilverStars;

	public bool Recognised { get; }

	public StarRatingResult(float goldStars, float silverStars, bool recognised)
	{
		GoldStars = goldStars;
		SilverStars = silverStars;
		Recognised = recognised;
	}
}
