import pandas as pd
import numpy as np

def detect_competition(df: pd.DataFrame) -> str:
    """
    Automatycznie wykrywa rodzaj konkurencji Formuły Student na podstawie danych telemetrycznych.
    Opiera się na fizycznych właściwościach (czas trwania, dystans, dynamika kątowa).
    """
    
    # 1. Znalezienie odpowiednich kolumn
    time_col = next((c for c in df.columns if 'czas' in c.lower() or 'time' in c.lower() or 'timestamp' in c.lower()), None)
    speed_col = next((c for c in df.columns if 'speed' in c.lower()), None)
    yaw_col = next((c for c in df.columns if 'yaw' in c.lower()), None)

    # 2. Obliczenie czasu trwania w sekundach
    duration = 0
    if time_col:
        time_data = pd.to_numeric(df[time_col], errors='coerce').dropna().to_numpy()
        if len(time_data) > 0:
            # Zabezpieczenie: jeśli czas jest w ms (np. Unix Timestamp), zamieniamy na sekundy
            if time_data[-1] > 1e9:
                duration = (time_data[-1] - time_data[0]) / 1000.0
            else:
                duration = time_data[-1] - time_data[0]

    # Awaryjne obliczenie czasu z liczby klatek (zakładając typowe 100 Hz dla dataloggera)
    if duration <= 0:
        duration = len(df) / 100.0

    # 3. Obliczenie przejechanego dystansu (całkowanie prędkości)
    distance = 0
    if speed_col:
        speed_kmh = pd.to_numeric(df[speed_col], errors='coerce').fillna(0).to_numpy()
        speed_ms = speed_kmh / 3.6 # Konwersja na m/s
        dt = duration / len(df)    # Uśredniony krok czasowy
        distance = np.sum(speed_ms * dt)

    # 4. Maksymalna prędkość kątowa (czy bolid robił "bączki" / zacieśnione kółka)
    yaw_max = 0
    if yaw_col:
        yaw = pd.to_numeric(df[yaw_col], errors='coerce').fillna(0).to_numpy()
        # Odrzucamy 1% skrajnych pików (szumów), bierzemy 99-ty percentyl
        yaw_max = np.percentile(np.abs(yaw), 99) if len(yaw) > 0 else 0

    # --- LOGIKA DECYZYJNA (HEURYSTYKA FS) ---
    
    # ENDURANCE: Każdy przejazd powyżej 5 minut to na pewno Endurance (lub luźny test na torze)
    if duration > 300 or distance > 4000:
        return "Endurance"
        
    # SKIDPAD: Wyróżnia się ekstremalnym kręceniem (YawRate > 30 deg/s) na krótkim dystansie i czasie
    if yaw_max > 25 and duration < 60 and distance < 500:
        return "Skidpad"
        
    # ACCELERATION: Bardzo krótki przejazd, jazda prosto (niski Yaw), dystans poniżej 200m (doliczając hamowanie)
    if yaw_max < 25 and duration < 30 and distance < 250:
        return "Acceleration"

    # AUTOCROSS: Zostaje jako domyślny dla przejazdów rzędu 1-2 minut, dystans ~1km, sporo kręcenia
    return "Autocross"