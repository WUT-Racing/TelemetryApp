import pandas as pd
import numpy as np

def detect_competition(df: pd.DataFrame) -> str:
    """
    Automatycznie wykrywa rodzaj konkurencji Formuły Student.
    Wersja 2.0: Odporna na logi z symulatora i różnorodne nazewnictwo z ECU.
    """
    # Pomocnicza funkcja do cwanego szukania kolumn bez względu na wielkość liter i dodatki
    def find_col(keywords, exclude=[]):
        for c in df.columns:
            cl = c.lower()
            if any(k in cl for k in keywords) and not any(e in cl for e in exclude):
                return c
        return None

    # 1. Dokładniejsze szukanie kolumn (odporne na polskie i angielskie nazwy z loggera)
    time_col = find_col(['eksportu [s]', 'time [s]', 'timestamp [s]'], ['ms', 'lokalny'])
    if not time_col:
        time_col = find_col(['time', 'czas'], ['ms', 'lokalny', 'dwell', 'otwarcia'])
        
    speed_col = find_col(['vspd', 'speed [km/h] (0x401', 'fl speed'], [])
    if not speed_col:
        speed_col = find_col(['speed'], [])
        
    yaw_col = find_col(['z angle rate', 'angular rate, z', 'yaw'], [])

    # 2. Bezpieczne liczenie DT (kroków czasowych poszczególnych klatek w sekundach)
    dt_array = np.array([])
    if time_col:
        time_data = pd.to_numeric(df[time_col], errors='coerce').dropna().to_numpy()
        if len(time_data) > 1:
            dt_array = np.diff(time_data)
            dt_array = np.append(dt_array, dt_array[-1]) # Klonujemy ostatni, by wyrównać tablicę
            
    # Jeśli log był tak brudny, że nie udało się policzyć czasu, zakładamy stałe 100Hz
    if len(dt_array) != len(df):
        dt_array = np.full(len(df), 0.01) 

    # 3. Metryki dystansu i czasu jazdy
    distance = 0
    active_time = 0
    if speed_col:
        speed_kmh = pd.to_numeric(df[speed_col], errors='coerce').fillna(0).to_numpy()
        speed_ms = speed_kmh / 3.6
        distance = np.sum(speed_ms * dt_array) # Całka drogi
        
        # Zliczamy tylko czas, gdzie bolid jechał > 5 km/h
        active_mask = speed_kmh > 5.0
        active_time = np.sum(dt_array[active_mask]) if np.any(active_mask) else 0

    # 4. Analiza skrętów kierownicą i żyroskopu (MIESO ALGORYTMU)
    yaw_max = 0
    max_turn_deg = 0
    if yaw_col:
        yaw = pd.to_numeric(df[yaw_col], errors='coerce').fillna(0).to_numpy()
        yaw_max = np.percentile(np.abs(yaw), 99) if len(yaw) > 0 else 0
        
        # CAŁKA Z YAW RATE: Daje nam aktualny fizyczny kąt, w jakim "patrzy" bolid
        heading = np.cumsum(yaw * dt_array)
        if len(heading) > 0:
            # Sprawdzamy maksymalną "rozpiętość" wykręconego kąta
            max_turn_deg = np.max(heading) - np.min(heading)

    # --- LOGIKA DECYZYJNA ---
    
    # 1. ACCELERATION: Bardzo niski YawRate (poniżej 15 deg/s), kierowca jedzie po prostej.
    if yaw_max < 15 and distance < 1500:
        return "Acceleration"

    # 2. SKIDPAD: Bolid wykonuje pełne okręgi.
    # Jeśli pojazd obrócił się wokół własnej osi o przynajmniej 320 stopni 
    # (z małym marginesem błędu na poślizg) jest to na 100% wariant Skidpada.
    if max_turn_deg > 320:
        return "Skidpad"
        
    # 3. ENDURANCE: Długi dystans w trakcie aktywnej jazdy.
    if distance > 4000 or active_time > 300:
        return "Endurance"

    # 4. AUTOCROSS: Zostaje jako domyślna dyscyplina torowa, gdzie są zakręty, ale nie są to okręgi.
    return "Autocross"