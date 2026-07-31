using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode.FastUnit.UI;

/// <summary>
/// 9-slice 프레임 텍스처가 <b>여백 없이 트림된 상태</b>인지 지킨다.
///
/// 2026-07-31 에 Town 허브의 서비스 카드·전과 행·비망록 항목 열두 개가 전부
/// "얇은 적갈색 빈 상자"로 보였다. 원인은 USS 도 레이아웃도 아니었다.
/// <c>ui_card_frame_normal.png</c> 과 <c>ui_icon_slot_frame.png</c> 이
/// <b>1517x1024</b> 로 저장돼 있었고, 실제 액자 그림은 좌 279px · 상 80px 안쪽에서야 시작했다.
/// USS 는 형제 텍스처(1024x1024, 밴드 ~56)에서 복사한 <c>-unity-slice: 56/64</c> 를 쓰고 있었으므로
/// 슬라이스가 <b>투명 여백만</b> 잘라냈고, 액자 그림 전체가 <b>중앙 타일</b>에 들어가
/// 요소 폭만큼 늘어났다. 그 늘어난 액자가 화면에서는 빈 상자로 읽혔다.
///
/// 트림 후 크기를 계약으로 건다. 원본(여백 포함) 아트가 다시 임포트되면 크기가 바뀌므로
/// 여기서 잡힌다. 슬라이스 값 자체는 <see cref="TownScreen"/> USS 가 소유한다.
///
/// 형제 텍스처가 왜 멀쩡했는지도 같이 남긴다 — 1024x1024 정사각에 밴드 38~59 라
/// 56/64 슬라이스가 우연히 맞았다. <b>같은 폴더의 값을 복사하는 것이 안전하지 않다.</b>
/// </summary>
[Category("FastUnit")]
public sealed class NineSliceFrameTextureFastTests
{
    private const string ArtBibleRoot = "Assets/_Game/UI/Foundation/Sprites/ArtBible";

    private static readonly IReadOnlyDictionary<string, (int Width, int Height)> ExpectedSizes =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            // 트림 완료 — 액자 그림이 텍스처 가장자리에 붙어 있다.
            ["Frame/ui_card_frame_normal.png"] = (923, 869),
            ["IconSlot/ui_icon_slot_frame.png"] = (928, 878),

            // 원래부터 정사각 + 얇은 여백이라 손대지 않았다. 회귀 감시용으로 같이 건다.
            ["Frame/ui_card_frame_locked.png"] = (1024, 1024),
            ["Frame/ui_card_frame_selected_base.png"] = (1024, 1024),
            ["Frame/ui_panel_frame_inner.png"] = (1024, 1024),
            ["Frame/ui_panel_frame_outer.png"] = (1024, 1024),
        };

    [Test]
    public void NineSliceFrames_StayTrimmed_SoSliceValuesKeepMeaning()
    {
        foreach (var (relativePath, expected) in ExpectedSizes)
        {
            var path = Path.Combine(ArtBibleRoot, relativePath).Replace('\\', '/');
            Assert.That(File.Exists(path), Is.True, $"9-slice 프레임 텍스처가 없습니다: {path}");

            var actual = ReadPngSize(path);
            Assert.That(
                actual,
                Is.EqualTo(expected),
                $"'{relativePath}' 크기가 {actual.Width}x{actual.Height} 입니다(기대 {expected.Width}x{expected.Height}).\n" +
                "9-slice 텍스처는 투명 여백이 없어야 -unity-slice 값이 실제 액자 밴드를 가리킵니다.\n" +
                "여백이 있는 원본을 다시 임포트했다면, 알파 bbox 로 트림한 뒤 TownScreen.uss 의 " +
                "-unity-slice-* 값을 새 밴드 두께로 맞추세요. 그렇지 않으면 액자가 중앙 타일에 들어가 " +
                "요소 폭만큼 늘어나고, 화면에는 얇은 빈 상자로 보입니다.");
        }
    }

    /// <summary>PNG IHDR 에서 폭/높이만 읽는다 — 에디터 API 없이 FastUnit 레인에서 돈다.</summary>
    private static (int Width, int Height) ReadPngSize(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        var signature = reader.ReadBytes(8);
        Assert.That(signature.Length, Is.EqualTo(8), $"PNG 시그니처를 읽지 못했습니다: {path}");
        Assert.That(signature[1], Is.EqualTo((byte)'P'), $"PNG 파일이 아닙니다: {path}");

        reader.ReadBytes(4); // IHDR length
        reader.ReadBytes(4); // "IHDR"
        var width = ReadBigEndianInt32(reader);
        var height = ReadBigEndianInt32(reader);
        return (width, height);
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }
}
