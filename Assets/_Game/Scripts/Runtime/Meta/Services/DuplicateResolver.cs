using SM.Core.Contracts;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class DuplicateResolver
{
    public static bool TryResolveDuplicate(
        bool alreadyOwned,
        RecruitTier tier,
        DuplicateEchoValueTable valueTable,
        out DuplicateConversionResult result)
    {
        result = new DuplicateConversionResult
        {
            SourceTier = tier,
            EchoGranted = valueTable.GetValue(tier),
        };
        return alreadyOwned;
    }
}
