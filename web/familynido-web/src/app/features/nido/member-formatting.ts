import { FamilyMember, MemberType } from '../../core/models/family-member';

/**
 * Localized label for a {@link MemberType}. Lives in a shared module so the
 * roster, the per-member detail page and any future selector all surface the
 * same words.
 */
export function memberTypeLabel(type: MemberType): string {
  switch (type) {
    case 'Adult':
      return $localize`:@@member.type.adult:Adulto`;
    case 'Child':
      return $localize`:@@member.type.child:Niño`;
    case 'Other':
      return $localize`:@@member.type.other:Otro`;
  }
}

/**
 * "X años" for adults/older kids, "Y meses" for the first year. Shared helper
 * because the same phrasing appears in the roster row and in the detail
 * header — drift between the two would be a tiny annoyance, easier to avoid
 * by reusing.
 */
export function yearsOldLabel(birthDate: string): string {
  const birth = new Date(birthDate);
  const diffMs = Date.now() - birth.getTime();
  const years = Math.floor(diffMs / (365.25 * 24 * 3600 * 1000));
  if (years < 1) {
    const months = Math.max(0, Math.floor(diffMs / (30.44 * 24 * 3600 * 1000)));
    return $localize`:@@member.age.months:${months}:N: meses`;
  }
  return $localize`:@@member.age.years:${years}:N: años`;
}

/** "Adulto · 38 años" / "Niño" when no birthDate. */
export function memberSubtitle(member: FamilyMember): string {
  const type = memberTypeLabel(member.memberType);
  return member.birthDate ? `${type} · ${yearsOldLabel(member.birthDate)}` : type;
}
