import React, { createContext, useContext, useMemo, useState } from 'react';
import { SafeAreaView, ScrollView, StyleSheet, Switch, Text, TouchableOpacity, View } from 'react-native';
import { light, dark, Theme } from './src/theme';
import {
  calcRows, demoSheet, demoSource, demoTranscript, evalExpr, fmt, library,
  simulatedInference, simulatedSpeech, CodeReview, SheetSection, TranscriptChunk,
} from './src/services';

const T = createContext<Theme>(light);
const useTheme = () => useContext(T);

// ---------- Composants communs ----------
const Card = ({ children, style }: any) => {
  const t = useTheme();
  return <View style={[s.card, { backgroundColor: t.card, borderColor: t.stroke }, style]}>{children}</View>;
};
const Pill = ({ text, bg, fg }: { text: string; bg: string; fg: string }) => (
  <View style={[s.pill, { backgroundColor: bg }]}><Text style={[s.pillText, { color: fg }]}>{text}</Text></View>
);
const Overline = ({ children }: any) => {
  const t = useTheme();
  return <Text style={{ color: t.inkFaint, fontSize: 10.5, fontWeight: '700', letterSpacing: 1.3 }}>{children}</Text>;
};
const AccentButton = ({ label, onPress, disabled }: any) => {
  const t = useTheme();
  return (
    <TouchableOpacity onPress={onPress} disabled={disabled}
      style={[s.accentBtn, { backgroundColor: t.accent, opacity: disabled ? 0.5 : 1 }]}>
      <Text style={{ color: '#FFF7F2', fontWeight: '700', fontSize: 13 }}>{label}</Text>
    </TouchableOpacity>
  );
};
const Ghost = ({ label, onPress }: any) => {
  const t = useTheme();
  return <TouchableOpacity onPress={onPress}><Text style={{ color: t.accentInk, fontWeight: '600', fontSize: 12.5 }}>{label}</Text></TouchableOpacity>;
};

// ---------- Écran : Accueil ----------
function Accueil({ go }: { go: (k: string) => void }) {
  const t = useTheme();
  const cards = [
    { tag: 'RELECTURE', title: 'facture.ts — 3 points relus', body: 'TVA en dur, arrondi par ligne, cas du tableau vide.', nav: 'code', cta: 'Ouvrir Studio →' },
    { tag: 'CALCULS', title: '1 écart détecté', body: '« 14 900 € » lu ; le recalcul donne 14 632 €. À trancher.', nav: 'calculs', cta: 'Voir l\'écart →' },
    { tag: 'ÉTUDES', title: 'Analyse, séance 8', body: 'Transcription 42 min prête. La fiche peut être générée.', nav: 'transcriptions', cta: 'Ouvrir →' },
  ];
  const today = [
    ['09:30', 'TD Analyse — salle B204 (feuille 7 à rendre)', 'Cours'],
    ['14:00', 'Relecture facture.ts avec Nomi — 3 points à trancher', 'Studio'],
    ['17:30', 'Fiche IPP : relire la section « Conduite »', 'Études'],
  ];
  return (
    <ScrollView contentContainerStyle={s.page}>
      <Text style={[s.h1, { color: t.ink }]}>Bonjour.</Text>
      <Text style={{ color: t.inkSoft, marginBottom: 14 }}>Samedi 21 novembre — 3 choses méritent votre regard.</Text>
      {cards.map(c => (
        <Card key={c.tag} style={{ marginBottom: 12 }}>
          <Overline>{c.tag}</Overline>
          <Text style={[s.cardTitle, { color: t.ink, marginTop: 4 }]}>{c.title}</Text>
          <Text style={{ color: t.inkSoft, fontSize: 12.5, marginVertical: 6 }}>{c.body}</Text>
          <Ghost label={c.cta} onPress={() => go(c.nav)} />
        </Card>
      ))}
      <Card>
        <Overline>AUJOURD'HUI</Overline>
        {today.map(([time, what, tag]) => (
          <View key={time} style={{ flexDirection: 'row', alignItems: 'center', marginTop: 10, gap: 10 }}>
            <Text style={{ color: t.inkSoft, fontVariant: ['tabular-nums'], width: 44 }}>{time}</Text>
            <Text style={{ color: t.ink, flex: 1, fontSize: 13 }}>{what}</Text>
            <Pill text={tag} bg={t.accentSoft} fg={t.accentInk} />
          </View>
        ))}
      </Card>
      <View style={{ flexDirection: 'row', gap: 12, marginTop: 12 }}>
        {[['42:17', 'transcrites cette semaine'], ['3', 'diagnostics code en attente'], ['1', 'écart de calcul à trancher']].map(([n, l]) => (
          <Card key={l} style={{ flex: 1, padding: 12 }}>
            <Text style={{ color: t.accentInk, fontSize: 20, fontVariant: ['tabular-nums'] }}>{n}</Text>
            <Text style={{ color: t.inkSoft, fontSize: 11 }}>{l}</Text>
          </Card>
        ))}
      </View>
    </ScrollView>
  );
}

// ---------- Écran : Studio > Code ----------
function Code() {
  const t = useTheme();
  const [review, setReview] = useState<CodeReview | null>(null);
  const [busy, setBusy] = useState(false);
  const [applied, setApplied] = useState(false);
  const run = async () => {
    setBusy(true); setApplied(false);
    setReview(await simulatedInference.reviewCode(demoSource));
    setBusy(false);
  };
  const chip = (sev: string) =>
    sev === 'erreur' ? [t.errSoft, t.err] : sev === 'avertissement' ? [t.warnSoft, t.warn] : [t.infoSoft, t.info];
  return (
    <ScrollView contentContainerStyle={s.page}>
      <Card>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          <Text style={[s.cardTitle, { color: t.ink }]}>facture.ts</Text>
          <AccentButton label={busy ? 'Analyse…' : 'Analyser avec Nomi'} onPress={run} disabled={busy} />
        </View>
        <View style={[s.codeBox, { borderColor: t.stroke, marginTop: 12 }]}>
          <Text style={{ color: t.ink, fontFamily: 'monospace', fontSize: 11.5, lineHeight: 17 }}>{demoSource}</Text>
        </View>
      </Card>
      {!review && !busy &&
        <Card><Text style={{ color: t.inkSoft, fontSize: 12.5 }}>
          Lancez l'analyse : Nomi relit le fichier et propose des corrections — rien n'est appliqué sans vous.</Text>
          <View style={{ marginTop: 8 }}><Pill text="IA simulée — aucun modèle chargé" bg={t.accentSoft} fg={t.accentInk} /></View>
        </Card>}
      {review?.diagnostics.map(d => {
        const [bg, fg] = chip(d.severity);
        return (
          <Card key={d.line} style={{ marginTop: 10 }}>
            <View style={{ flexDirection: 'row', gap: 8, alignItems: 'center' }}>
              <Pill text={d.severity.toUpperCase()} bg={bg} fg={fg} />
              <Text style={{ color: t.inkSoft, fontFamily: 'monospace', fontSize: 11 }}>ligne {d.line}</Text>
            </View>
            <Text style={{ color: t.ink, fontSize: 13, marginTop: 6 }}>{d.message}</Text>
            <Text style={{ color: t.inkSoft, fontSize: 12, marginTop: 4 }}>→ {d.suggestion}</Text>
          </Card>
        );
      })}
      {review && !applied &&
        <Card style={{ marginTop: 10 }}>
          <Overline>CORRECTIF PROPOSÉ — NON APPLIQUÉ</Overline>
          <View style={[s.codeBox, { backgroundColor: t.errSoft, marginTop: 8, borderColor: t.stroke }]}>
            <Text style={{ color: t.err, fontFamily: 'monospace', fontSize: 11.5 }}>{review.diffBefore}</Text>
          </View>
          <View style={[s.codeBox, { backgroundColor: t.okSoft, marginTop: 6, borderColor: t.stroke }]}>
            <Text style={{ color: t.ok, fontFamily: 'monospace', fontSize: 11.5 }}>{review.diffAfter}</Text>
          </View>
          <Text style={{ color: t.inkSoft, fontSize: 12, marginTop: 8 }}>{review.explanation}</Text>
          <View style={{ flexDirection: 'row', gap: 14, marginTop: 10 }}>
            <AccentButton label="Appliquer" onPress={() => setApplied(true)} />
            <Ghost label="Écarter" onPress={() => setReview(null)} />
          </View>
        </Card>}
      {applied &&
        <Card style={{ marginTop: 10 }}><Text style={{ color: t.ok, fontWeight: '600' }}>
          Correctif appliqué dans la vue — le fichier n'est pas modifié (prototype).</Text></Card>}
    </ScrollView>
  );
}

// ---------- Écran : Vérif. calculs ----------
function Calculs() {
  const t = useTheme();
  const [done, setDone] = useState(false);
  const rows = calcRows.map(r => {
    let computed = NaN, match = true;
    try { computed = evalExpr(r.expr); match = Math.abs(computed - r.lu) < 0.005; } catch { match = false; }
    return { ...r, computed, match };
  });
  return (
    <ScrollView contentContainerStyle={s.page}>
      <Text style={[s.h1, { color: t.ink, fontSize: 20 }]}>Vérification des calculs</Text>
      <Text style={{ color: t.inkSoft, fontSize: 12.5, marginBottom: 12 }}>
        Note de frais — novembre. L'IA extrait, le moteur déterministe recalcule.</Text>
      <Card style={{ padding: 0 }}>
        {rows.map((r, i) => (
          <View key={r.label} style={[s.tableRow, { borderColor: t.stroke, borderTopWidth: i ? 1 : 0 }]}>
            <Text style={{ color: t.ink, fontSize: 13, fontWeight: '600' }}>{r.label}</Text>
            <Text style={{ color: t.inkSoft, fontSize: 11.5, fontFamily: 'monospace' }}>{r.expr}</Text>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: 4 }}>
              <Text style={{ color: t.inkSoft, fontSize: 12 }}>lu {fmt(r.lu)} €</Text>
              <Text style={{ color: done && !r.match ? t.err : t.ink, fontSize: 12, fontFamily: 'monospace' }}>
                {done ? `recalculé ${fmt(r.computed)} €` : 'recalculé —'}
              </Text>
              <Pill text={done ? (r.match ? 'conforme' : 'écart') : 'à vérifier'}
                bg={done ? (r.match ? t.okSoft : t.errSoft) : 'rgba(128,128,128,0.15)'}
                fg={done ? (r.match ? t.ok : t.err) : t.inkSoft} />
            </View>
          </View>
        ))}
      </Card>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10, marginTop: 12 }}>
        <AccentButton label="Vérifier les calculs" onPress={() => setDone(true)} />
        <Pill text="moteur déterministe — réel" bg={t.okSoft} fg={t.ok} />
      </View>
    </ScrollView>
  );
}

// ---------- Écran : Transcription ----------
function Transcription() {
  const t = useTheme();
  const [chunks, setChunks] = useState<TranscriptChunk[]>([]);
  const [notes, setNotes] = useState<string[]>([]);
  const [timer, setTimer] = useState('00:00');
  const [running, setRunning] = useState(false);
  const run = async () => {
    if (running) return;
    setRunning(true); setChunks([]); setNotes([]);
    for await (const c of simulatedSpeech.streamTranscript()) {
      setTimer(`${String(Math.floor(c.endSec / 60)).padStart(2, '0')}:${String(c.endSec % 60).padStart(2, '0')}`);
      setChunks(prev => [...prev, c]);
      for await (const n of simulatedInference.streamLiveNotes(c)) setNotes(prev => [...prev, n]);
    }
    setRunning(false);
  };
  return (
    <ScrollView contentContainerStyle={s.page}>
      <Card>
        <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
          <View>
            <Text style={[s.cardTitle, { color: t.ink }]}>Analyse — séance 8</Text>
            <Text style={{ color: t.inkSoft, fontSize: 11.5 }}>Local — rien ne quitte la machine</Text>
          </View>
          <Text style={{ color: t.accentInk, fontFamily: 'monospace', fontSize: 18 }}>{timer}</Text>
        </View>
        <AccentButton label={running ? 'Écoute…' : 'Démarrer la transcription'} onPress={run} disabled={running} />
      </Card>
      <Card style={{ marginTop: 12 }}>
        <Overline>TRANSCRIPTION</Overline>
        {chunks.length === 0 && <Text style={{ color: t.inkSoft, fontSize: 12.5, marginTop: 6 }}>
          « [inaudible] » et passages incertains marqués, pas inventés.</Text>}
        {chunks.map(c => (
          <View key={c.startSec} style={{ marginTop: 10 }}>
            <View style={{ flexDirection: 'row', gap: 6, alignItems: 'center' }}>
              <Pill text={c.speaker} bg={c.speaker === 'Prof' ? t.accentSoft : t.infoSoft} fg={c.speaker === 'Prof' ? t.accentInk : t.info} />
              <Text style={{ color: t.inkFaint, fontFamily: 'monospace', fontSize: 10.5 }}>
                {`${String(Math.floor(c.startSec / 60)).padStart(2, '0')}:${String(c.startSec % 60).padStart(2, '0')}`}
              </Text>
              {c.uncertain && <Pill text="incertain" bg={t.warnSoft} fg={t.warn} />}
            </View>
            <Text style={{ color: t.ink, fontSize: 13, marginTop: 3 }}>{c.text}</Text>
          </View>
        ))}
      </Card>
      <Card style={{ marginTop: 12 }}>
        <View style={{ flexDirection: 'row', gap: 8, alignItems: 'center' }}>
          <Overline>EN DIRECT</Overline>
          <Pill text="notes IA simulées" bg={t.accentSoft} fg={t.accentInk} />
        </View>
        {notes.map((n, i) => <Text key={i} style={{ color: t.ink, fontSize: 12.5, marginTop: 8 }}>{n}</Text>)}
      </Card>
    </ScrollView>
  );
}

// ---------- Écran : Fiche ----------
function Fiche() {
  const t = useTheme();
  const [sections, setSections] = useState<SheetSection[]>([]);
  const [busy, setBusy] = useState(false);
  const run = async () => {
    setBusy(true); setSections([]);
    setSections((await simulatedInference.buildRevisionSheet('transcription IPP')).sections);
    setBusy(false);
  };
  const prov = (p: string): [string, string, string] =>
    p === 'nomi' ? [t.infoSoft, t.info, 'Explication Nomi'] : p === 'hypothese' ? [t.warnSoft, t.warn, 'Hypothèse'] : [t.accentSoft, t.accentInk, 'Cours'];
  const sources = [
    ['« poids inférieur au 10ᵉ percentile de l\'âge gestationnel »', '12:04'],
    ['« tabac : premier facteur évitable »', '18:37'],
    ['« insuffisance placentaire — mécanisme le plus fréquent »', '22:10'],
    ['« doppler ombilical si suspicion »', '31:02'],
  ];
  return (
    <ScrollView contentContainerStyle={s.page}>
      <Card>
        <Text style={[s.cardTitle, { color: t.ink }]}>Fiche de révision</Text>
        <Text style={{ color: t.inkSoft, fontSize: 12, marginVertical: 4 }}>
          Depuis « Cours — IPP » (38 min). Chaque section indique sa provenance.</Text>
        <AccentButton label={busy ? 'Génération…' : sections.length ? 'Régénérer' : 'Générer la fiche'} onPress={run} disabled={busy} />
      </Card>
      {sections.map(sec => {
        const [bg, fg, label] = prov(sec.provenance);
        return (
          <Card key={sec.title} style={{ marginTop: 10 }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
              <Text style={[s.cardTitle, { color: t.ink, fontSize: 14 }]}>{sec.title}</Text>
              <Pill text={label} bg={bg} fg={fg} />
            </View>
            <Text style={{ color: t.ink, fontSize: 12.5, marginTop: 6 }}>{sec.body}</Text>
          </Card>
        );
      })}
      <Card style={{ marginTop: 12 }}>
        <Overline>SOURCES DU COURS</Overline>
        {sources.map(([text, stamp]) => (
          <View key={stamp} style={{ marginTop: 8 }}>
            <Text style={{ color: t.inkSoft, fontSize: 12, fontStyle: 'italic' }}>{text}</Text>
            <Text style={{ color: t.inkFaint, fontSize: 10, letterSpacing: 1 }}>cours — {stamp}</Text>
          </View>
        ))}
      </Card>
    </ScrollView>
  );
}

// ---------- Écran : Bibliothèque ----------
function Bibliotheque({ go }: { go: (k: string) => void }) {
  const t = useTheme();
  return (
    <ScrollView contentContainerStyle={s.page}>
      <Text style={[s.h1, { color: t.ink, fontSize: 20 }]}>Bibliothèque</Text>
      <Text style={{ color: t.inkSoft, fontSize: 12.5, marginBottom: 12 }}>Stockés chiffrés sur cette machine (DPAPI).</Text>
      {library.map(item => (
        <Card key={item.title} style={{ marginBottom: 10 }}>
          <Pill text={item.kind.toUpperCase()} bg={t.accentSoft} fg={t.accentInk} />
          <Text style={[s.cardTitle, { color: t.ink, fontSize: 14, marginTop: 6 }]}>{item.title}</Text>
          <Text style={{ color: t.inkSoft, fontSize: 12, marginVertical: 4 }}>{item.meta}</Text>
          <Ghost label="Ouvrir →" onPress={() => go(item.nav)} />
        </Card>
      ))}
    </ScrollView>
  );
}

// ---------- Écran : Paramètres ----------
function Parametres({ darkMode, setDark }: { darkMode: boolean; setDark: (v: boolean) => void }) {
  const t = useTheme();
  const models = [
    ['Qwen3 8B', 'Raisonnement, réponses — ~5 Go', 'Chargé', t.okSoft, t.ok],
    ['Gemma 4 E4B', 'Vision — ~6 Go', 'Présent', t.infoSoft, t.info],
    ['Whisper small', 'Voix → texte — ~488 Mo', 'À télécharger', t.warnSoft, t.warn],
  ];
  const rows = [
    ['Vitesse de lecture', 'Synthèse vocale — 1,0× (simulé)'],
    ['Nombres', 'Arrondi bancaire à 2 décimales — réel'],
    ['Données', 'Chiffrées DPAPI — rien ne part en ligne'],
  ];
  return (
    <ScrollView contentContainerStyle={s.page}>
      <Text style={[s.h1, { color: t.ink, fontSize: 20 }]}>Paramètres</Text>
      <Card style={{ marginTop: 12 }}>
        <Overline>MODÈLES EMBARQUÉS</Overline>
        {models.map(([n, d, st, bg, fg]) => (
          <View key={n as string} style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: 10 }}>
            <View style={{ flex: 1 }}>
              <Text style={{ color: t.ink, fontWeight: '600', fontSize: 13 }}>{n}</Text>
              <Text style={{ color: t.inkSoft, fontSize: 11.5 }}>{d}</Text>
            </View>
            <Pill text={st as string} bg={bg as string} fg={fg as string} />
          </View>
        ))}
        <Text style={{ color: t.inkSoft, fontSize: 11, marginTop: 10 }}>États simulés — aucun modèle chargé.</Text>
      </Card>
      <Card style={{ marginTop: 12 }}>
        <Overline>INTERFACE</Overline>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: 10 }}>
          <Text style={{ color: t.ink, fontSize: 13 }}>Thème sombre</Text>
          <Switch value={darkMode} onValueChange={setDark} trackColor={{ true: t.accent, false: t.stroke }} />
        </View>
      </Card>
      <Card style={{ marginTop: 12 }}>
        <Overline>LECTURE</Overline>
        {rows.map(([l, d]) => (
          <View key={l} style={{ marginTop: 10 }}>
            <Text style={{ color: t.ink, fontWeight: '600', fontSize: 13 }}>{l}</Text>
            <Text style={{ color: t.inkSoft, fontSize: 11.5 }}>{d}</Text>
          </View>
        ))}
      </Card>
    </ScrollView>
  );
}

// ---------- Coquille ----------
const NAV: { group?: string; key: string; label: string }[] = [
  { key: 'accueil', label: 'Accueil' },
  { group: 'Journée', key: 'aujourdhui', label: 'Aujourd\'hui' },
  { key: 'tableau', label: 'Tableau' },
  { key: 'routines', label: 'Routines' },
  { group: 'Studio', key: 'code', label: 'Code' },
  { key: 'calculs', label: 'Vérif. calculs' },
  { key: 'focus', label: 'Focus' },
  { group: 'Études', key: 'cours', label: 'Cours' },
  { key: 'transcriptions', label: 'Transcriptions' },
  { key: 'fiches', label: 'Fiches' },
  { key: 'bibliotheque', label: 'Bibliothèque' },
  { key: 'parametres', label: 'Paramètres' },
];
const IMPLEMENTED = ['accueil', 'code', 'calculs', 'transcriptions', 'fiches', 'bibliotheque', 'parametres'];

export default function App() {
  const [screen, setScreen] = useState('accueil');
  const [darkMode, setDark] = useState(false);
  const t = darkMode ? dark : light;
  const render = () => {
    switch (screen) {
      case 'accueil': return <Accueil go={setScreen} />;
      case 'code': return <Code />;
      case 'calculs': return <Calculs />;
      case 'transcriptions': return <Transcription />;
      case 'fiches': return <Fiche />;
      case 'bibliotheque': return <Bibliotheque go={setScreen} />;
      case 'parametres': return <Parametres darkMode={darkMode} setDark={setDark} />;
      default:
        return (
          <View style={s.page}>
            <Card>
              <Text style={[s.cardTitle, { color: t.ink }]}>{NAV.find(n => n.key === screen)?.label}</Text>
              <Text style={{ color: t.inkSoft, fontSize: 12.5, marginTop: 6 }}>
                Écran dessiné dans la maquette v4 — non implémenté dans ce prototype.</Text>
            </Card>
          </View>
        );
    }
  };
  return (
    <T.Provider value={t}>
      <SafeAreaView style={{ flex: 1, backgroundColor: t.canvas }}>
        <View style={[s.topbar, { borderColor: t.stroke }]}>
          <View style={[s.logo, { backgroundColor: t.accent }]}><Text style={{ color: '#FFF7F2', fontWeight: '800' }}>N</Text></View>
          <Text style={{ color: t.ink, fontWeight: '800', fontSize: 15, marginRight: 8 }}>Nomi</Text>
          <Pill text="maquette — données de démonstration" bg={t.accentSoft} fg={t.accentInk} />
        </View>
        <ScrollView horizontal showsHorizontalScrollIndicator={false}
          style={{ flexGrow: 0, borderBottomWidth: 1, borderColor: t.stroke }}
          contentContainerStyle={{ padding: 8, gap: 6, alignItems: 'center' }}>
          {NAV.map(n => {
            const sel = screen === n.key;
            const soon = !IMPLEMENTED.includes(n.key);
            return (
              <TouchableOpacity key={n.key} onPress={() => setScreen(n.key)}
                style={[s.navItem, sel && { backgroundColor: t.accentSoft }]}>
                <Text style={{ color: sel ? t.accentInk : t.inkSoft, fontSize: 12.5, fontWeight: sel ? '700' : '500' }}>
                  {n.label}{soon ? ' ·' : ''}
                </Text>
              </TouchableOpacity>
            );
          })}
        </ScrollView>
        <View style={{ flex: 1 }}>{render()}</View>
      </SafeAreaView>
    </T.Provider>
  );
}

const s = StyleSheet.create({
  page: { padding: 16, paddingBottom: 40 },
  h1: { fontSize: 24, fontWeight: '800', marginBottom: 2 },
  card: { borderRadius: 16, borderWidth: 1, padding: 16 },
  cardTitle: { fontSize: 15, fontWeight: '700' },
  pill: { borderRadius: 999, paddingHorizontal: 10, paddingVertical: 3, alignSelf: 'flex-start' },
  pillText: { fontSize: 11, fontWeight: '700' },
  accentBtn: { borderRadius: 12, paddingHorizontal: 16, paddingVertical: 9, alignSelf: 'flex-start', marginTop: 8 },
  codeBox: { borderWidth: 1, borderRadius: 10, padding: 10 },
  tableRow: { padding: 12 },
  topbar: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 14, paddingVertical: 10, borderBottomWidth: 1 },
  logo: { width: 24, height: 24, borderRadius: 6, alignItems: 'center', justifyContent: 'center' },
  navItem: { paddingHorizontal: 12, paddingVertical: 7, borderRadius: 10 },
});
