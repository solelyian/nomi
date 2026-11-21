// Design system Nomi v4 « verre chaud » — voir docs/refonte/dossier.md §7.
export const light = {
  canvas: '#ECE6DE', ink: '#211D19', inkSoft: '#6B6158', inkFaint: '#9B9187',
  accent: '#B4482B', accentInk: '#8C351F', accentSoft: 'rgba(180,72,43,0.14)',
  card: 'rgba(255,255,255,0.76)', stroke: 'rgba(33,29,25,0.12)',
  ok: '#3E7C4F', okSoft: 'rgba(62,124,79,0.15)',
  warn: '#B07B22', warnSoft: 'rgba(176,123,34,0.15)',
  err: '#B4482B', errSoft: 'rgba(180,72,43,0.15)',
  info: '#4A6E8C', infoSoft: 'rgba(74,110,140,0.15)',
};
export const dark = {
  canvas: '#0F0E0D', ink: '#F0EBE4', inkSoft: '#ABA195', inkFaint: '#7A7166',
  accent: '#E8825F', accentInk: '#F2A37F', accentSoft: 'rgba(232,130,95,0.16)',
  card: 'rgba(255,255,255,0.08)', stroke: 'rgba(240,235,228,0.15)',
  ok: '#7FBA8E', okSoft: 'rgba(127,186,142,0.18)',
  warn: '#D9A54A', warnSoft: 'rgba(217,165,74,0.18)',
  err: '#E8825F', errSoft: 'rgba(232,130,95,0.18)',
  info: '#7FA3BF', infoSoft: 'rgba(127,163,191,0.18)',
};
export type Theme = typeof light;
export const font = 'AtkinsonHyperlegible';
