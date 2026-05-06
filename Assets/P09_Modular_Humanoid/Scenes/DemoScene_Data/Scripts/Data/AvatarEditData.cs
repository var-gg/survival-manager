namespace P09.Modular.Humanoid.Data
{
    [System.Serializable]
    public class AvatarEditData
    {
        public int WeaponId { get; set; } = 0;
        public int ShieldId { get; set; } = 0;
        public int HeadId { get; set; } = 0;
        public int ChestId { get; set; } = 0;
        public int ArmId { get; set; } = 0;
        public int WaistId { get; set; } = 0;
        public int LegId { get; set; } = 0;
        public int SexId { get; set; } = 1;
        public int FaceTypeId { get; set; } = 1;
        public int HairStyleId { get; set; } = 1;
        public int HairColorId { get; set; } = 1;
        public int SkinId { get; set; } = 1;
        public int EyeColorId { get; set; } = 1;
        public int FacialHairId { get; set; } = 0;
        public int BustSizeId { get; set; } = 2;

        public void SetId(EditPartType editPartType, int id)
        {
            if (id < 0) return;

            var _ = editPartType switch
            {
                EditPartType.Weapon => WeaponId = id,
                EditPartType.Shield => ShieldId = id,
                EditPartType.Head => HeadId = id,
                EditPartType.Chest => ChestId = id,
                EditPartType.Arm => ArmId = id,
                EditPartType.Waist => WaistId = id,
                EditPartType.Leg => LegId = id,
                EditPartType.Sex => SexId = id,
                EditPartType.HairStyle => HairStyleId = id,
                EditPartType.HairColor => HairColorId = id,
                EditPartType.Skin => SkinId = id,
                EditPartType.EyeColor => EyeColorId = id,
                EditPartType.FacialHair => FacialHairId = id,
                EditPartType.BustSize => BustSizeId = id,
                EditPartType.FaceType => FaceTypeId = id,
                _ => throw new System.ArgumentException("Invalid EditPartType", nameof(editPartType))
            };
        }                

        public int GetCurrentId(EditPartType editPartType)
        {
            return editPartType switch
            {
                EditPartType.Weapon => WeaponId,
                EditPartType.Shield => ShieldId,
                EditPartType.Head => HeadId,
                EditPartType.Chest => ChestId,
                EditPartType.Arm => ArmId,
                EditPartType.Waist => WaistId,
                EditPartType.Leg => LegId,
                EditPartType.Sex => SexId,
                EditPartType.HairStyle => HairStyleId,
                EditPartType.HairColor => HairColorId,
                EditPartType.Skin => SkinId,
                EditPartType.EyeColor => EyeColorId,
                EditPartType.FacialHair => FacialHairId,
                EditPartType.BustSize => BustSizeId,
                EditPartType.FaceType => FaceTypeId,
                _ => 0
            };
        }
    }
}