// Hero ★ ascension perks (§5.5) — shown in the ascend upgrade modal and on the Masteries screen.
// Each ★ also multiplies the hero's base stats by ~+8% (StatResolver.StarMult = 1.08^stars).

export interface HeroPerk { star: number; text: string; soon?: boolean }

export const HERO_PERKS: Record<string, HeroPerk[]> = {
  fire_mage: [
    { star: 1, text: "+5% Crit Chance" },
    { star: 3, text: "Ignited blocks take +15% from crits" },
    { star: 5, text: "A crit kill ignites a nearby block" },
  ],
  paladin: [
    { star: 1, text: "+0.2 Crit Damage" },
    { star: 3, text: "First ball-drain each level is saved" },
    { star: 5, text: "Below 50% HP, +25% Crit Damage" },
  ],
  engineer: [
    { star: 1, text: "+1 Tempo step" },
    { star: 3, text: "+1 starting ball" },
    { star: 5, text: "Extra balls deal full damage", soon: true },
  ],
  necromancer: [
    { star: 1, text: "Heal 1 HP per 60 kills" },
    { star: 3, text: "Crits drain mana to you" },
    { star: 5, text: "Full-combo kill may raise a helper-ball" },
  ],
};

/** The perk unlocked AT a given star (or null if that star grants no named perk). */
export function perkAtStar(heroId: string, star: number): HeroPerk | null {
  return (HERO_PERKS[heroId] ?? []).find(p => p.star === star) ?? null;
}

/** Per-star stat multiplier (mirrors HeroStats.StarMult). */
export const STAR_STAT_MULT = 1.08;
