// STRANGER 온라인 랭킹 (Vercel 서버리스 함수, npm 의존성 없음)
//   GET  /api/rank?diff=0           → { items: [{ time, team:[4], cats, date }] } 빠른 순 상위 20
//   POST /api/rank {diff,time,team,cats} → { ok:true, rank:n }
//   diff = 기록표 번호: 0~2 스토리(쉬움·보통·어려움) · 3~5 스피드런
// 저장소(먼저 있는 것을 쓴다):
//   1) Upstash Redis REST (UPSTASH_REDIS_REST_URL/TOKEN 또는 Vercel KV 의 KV_REST_API_URL/TOKEN)
//   2) Firebase Realtime Database REST (FIREBASE_URL, 없으면 TinyKitchen·TinyDigger 와 같은 DB)
//      배치: /s/stranger_d<diff>/<16자리 id> = { n: 대표 이름, s: 점수(높을수록 빠름), m: "시간;단풍;팀,팀,팀,팀;날짜", t: 서버 시각 }
//      (그 DB 규칙: id 16자 · s 정수 · n 12자 · m 60자 — TinyKitchen Rank.jslib 과 같은 모양)
// 개인정보: 게임 안 닉네임만 저장한다. IP 는 Upstash 에서 하루 제출 횟수를 세는 데만 쓰고(해시), 기록에는 넣지 않는다.

const crypto = require('crypto');

const URL_ = process.env.UPSTASH_REDIS_REST_URL || process.env.KV_REST_API_URL || '';
const TOKEN = process.env.UPSTASH_REDIS_REST_TOKEN || process.env.KV_REST_API_TOKEN || '';
const FB = (process.env.FIREBASE_URL || 'https://tiny-games-6009a-default-rtdb.asia-southeast1.firebasedatabase.app').replace(/\/+$/, '');
const KEEP = 200;          // 기록표마다 남길 기록 수(Upstash)
const TOP = 20;            // GET 으로 돌려줄 수
const DAILY_LIMIT = 30;    // IP 당 하루 제출 수(Upstash)
const BOARDS = 6;

async function redis(cmd) {
  const r = await fetch(URL_, {
    method: 'POST',
    headers: { Authorization: 'Bearer ' + TOKEN, 'Content-Type': 'application/json' },
    body: JSON.stringify(cmd),
  });
  if (!r.ok) throw new Error('redis ' + r.status);
  const j = await r.json();
  if (j.error) throw new Error(j.error);
  return j.result;
}

function cleanName(s) {
  s = String(s == null ? '' : s);
  s = s.replace(/https?:\/\/\S*/gi, '').replace(/www\.\S*/gi, '');
  // 한글 · 영문 · 숫자 · 공백만 남긴다(특수문자 · 주소 제거)
  s = s.replace(/[^0-9A-Za-zㄱ-ㆎ가-힣 ]/g, '').replace(/\s+/g, ' ').trim();
  if (s.length > 12) s = s.slice(0, 12);
  return s || '?';
}

function send(res, code, obj) {
  res.statusCode = code;
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  res.setHeader('Cache-Control', 'no-store');
  res.end(JSON.stringify(obj));
}

async function readBody(req) {
  if (req.body && typeof req.body === 'object') return req.body;
  if (typeof req.body === 'string') { try { return JSON.parse(req.body); } catch (e) { return null; } }
  return await new Promise((resolve) => {
    let d = '';
    req.on('data', (c) => { d += c; if (d.length > 4096) req.destroy(); });
    req.on('end', () => { try { resolve(JSON.parse(d)); } catch (e) { resolve(null); } });
    req.on('error', () => resolve(null));
  });
}

// ---------------------------------------------------------------- Firebase 저장소
const fbBoard = (diff) => FB + '/s/stranger_d' + diff;
const toScore = (time) => Math.max(1, 40000 - Math.round(time * 10));   // 높을수록 빠름(그 DB 는 큰 점수가 위)

function packMeta(entry) {
  // m 은 60자 안: 이름이 길면 팀 이름을 줄인다
  for (let cut = 12; cut >= 2; cut--) {
    const m = entry.time + ';' + entry.cats + ';' + entry.team.map((t) => t.slice(0, cut)).join(',') + ';' + entry.date;
    if (m.length <= 60) return m;
  }
  return entry.time + ';' + entry.cats + ';AI,AI,AI,AI;' + entry.date;
}

function unpack(v) {
  if (!v || typeof v.s !== 'number' || typeof v.m !== 'string') return null;
  const p = v.m.split(';');
  const time = Number(p[0]);
  if (!(time > 0)) return null;
  const team = (p[2] || '').split(',').slice(0, 4);
  while (team.length < 4) team.push('AI');
  return { time, team, cats: Math.max(0, Math.min(99, parseInt(p[1], 10) || 0)), date: p[3] || '' };
}

async function fbList(diff) {
  const r = await fetch(fbBoard(diff) + '.json?orderBy=%22s%22&limitToLast=200');
  if (!r.ok) throw new Error('firebase ' + r.status);
  const o = (await r.json()) || {};
  const items = [];
  for (const k in o) { const e = unpack(o[k]); if (e) { e.id = k; items.push(e); } }
  items.sort((a, b) => a.time - b.time);
  return items;
}

async function fbAdd(diff, entry) {
  const id = crypto.randomBytes(8).toString('hex');   // 16자리
  const lead = entry.team.find((t) => t !== 'AI') || 'AI';
  const payload = { n: lead.slice(0, 12), s: toScore(entry.time), m: packMeta(entry), t: { '.sv': 'timestamp' } };
  const r = await fetch(fbBoard(diff) + '/' + id + '.json', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
  if (!r.ok) throw new Error('firebase put ' + r.status);
  const list = await fbList(diff);
  const i = list.findIndex((e) => e.id === id);
  return i < 0 ? -1 : i + 1;
}

// ---------------------------------------------------------------- 요청
module.exports = async (req, res) => {
  const useRedis = !!(URL_ && TOKEN);
  try {
    if (req.method === 'GET') {
      const q = new URL(req.url, 'http://x').searchParams;
      const diff = Math.max(0, Math.min(BOARDS - 1, parseInt(q.get('diff') || '0', 10) || 0));
      if (!useRedis) {
        const items = (await fbList(diff)).slice(0, TOP).map(({ time, team, cats, date }) => ({ time, team, cats, date }));
        return send(res, 200, { items });
      }
      const list = (await redis(['ZRANGE', 'rank:d' + diff, 0, TOP - 1])) || [];
      const items = [];
      for (const m of list) { try { items.push(JSON.parse(m)); } catch (e) {} }
      return send(res, 200, { items });
    }
    if (req.method === 'POST') {
      const b = await readBody(req);
      if (!b) return send(res, 400, { error: 'bad body' });
      const diff = parseInt(b.diff, 10);
      const time = Number(b.time);
      const cats = Math.max(0, Math.min(99, parseInt(b.cats, 10) || 0));
      if (!(diff >= 0 && diff < BOARDS)) return send(res, 400, { error: 'diff' });
      if (!(time >= 20 && time <= 3600)) return send(res, 400, { error: 'time' });
      if (!Array.isArray(b.team) || b.team.length < 1 || b.team.length > 4) return send(res, 400, { error: 'team' });
      const team = b.team.slice(0, 4).map(cleanName);
      while (team.length < 4) team.push('AI');
      const day = new Date().toISOString().slice(0, 10);
      const entry = { time: Math.round(time * 10) / 10, team, cats, date: day };

      if (!useRedis) return send(res, 200, { ok: true, rank: await fbAdd(diff, entry) });

      // 하루 IP 당 제출 제한(IP 는 해시로만)
      const ip = String(req.headers['x-forwarded-for'] || req.socket?.remoteAddress || '').split(',')[0].trim();
      const rl = 'rl:' + day + ':' + crypto.createHash('sha256').update(ip + day).digest('hex').slice(0, 16);
      const n = await redis(['INCR', rl]);
      if (n === 1) await redis(['EXPIRE', rl, 90000]);
      if (n > DAILY_LIMIT) return send(res, 429, { error: 'too many' });

      entry.id = crypto.randomBytes(4).toString('hex');
      const key = 'rank:d' + diff;
      await redis(['ZADD', key, entry.time, JSON.stringify(entry)]);
      await redis(['ZREMRANGEBYRANK', key, KEEP, -1]);
      const rank = await redis(['ZRANK', key, JSON.stringify(entry)]);
      return send(res, 200, { ok: true, rank: rank == null ? -1 : rank + 1 });
    }
    res.setHeader('Allow', 'GET, POST');
    return send(res, 405, { error: 'method' });
  } catch (e) {
    return send(res, 500, { error: 'server' });
  }
};
