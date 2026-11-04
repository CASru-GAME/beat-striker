"""Generate tutorial MP3s using a running VOICEVOX engine and ffmpeg."""
import argparse
import json
import subprocess
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import Request, urlopen


def post(engine, endpoint, params, body=None):
    request = Request(engine.rstrip('/') + endpoint + '?' + urlencode(params),
                      data=body if body is not None else b'', method='POST')
    if body is not None:
        request.add_header('Content-Type', 'application/json')
    with urlopen(request, timeout=120) as response:
        return response.read()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--engine', default='http://127.0.0.1:50021')
    parser.add_argument('--overwrite', action='store_true', help='Regenerate existing MP3s.')
    parser.add_argument('--ids', nargs='+', help='Generate only the named clips.')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    manifest = json.loads((root / 'voice-lines.json').read_text(encoding='utf-8'))
    for clip in manifest['Clips']:
        if args.ids and clip['Id'] not in args.ids:
            continue
        target = root / (clip['Id'] + '.mp3')
        if target.exists() and not args.overwrite:
            continue
        query = json.loads(post(args.engine, '/audio_query',
                               {'text': clip['Text'], 'speaker': manifest['Speaker']}))
        query.update(speedScale=1.05, prePhonemeLength=0.035, postPhonemeLength=0.035,
                     outputSamplingRate=24000, outputStereo=False)
        data = post(args.engine, '/synthesis', {'speaker': manifest['Speaker']},
                    json.dumps(query, ensure_ascii=False).encode('utf-8'))
        result = subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-f', 'wav',
                                 '-i', 'pipe:0', '-af', 'volume=2,alimiter=limit=0.90:level=false',
                                 '-codec:a', 'libmp3lame', '-b:a', '128k',
                                 '-f', 'mp3', 'pipe:1'], input=data, stdout=subprocess.PIPE,
                                check=True)
        target.write_bytes(result.stdout)
        print(target.name, flush=True)


if __name__ == '__main__':
    main()
