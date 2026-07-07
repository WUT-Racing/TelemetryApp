import pandas as pd
import numpy as np
import kinematics
import track_geometry

def process(df: pd.DataFrame, config: dict) -> dict:
    """
    Główna funkcja spinająca logikę Skidpada.
    Przyjmuje DataFrame z danymi i zwraca gotowe metryki jako słownik.
    """
    # Sprytna funkcja szukająca kolumn, znana z naszego detektora
    def find_col(keywords, exclude=[]):
        for c in df.columns:
            cl = c.lower()
            if any(k in cl for k in keywords) and not any(e in cl for e in exclude):
                return c
        return None

    # 1. Automatyczne znalezienie kolumn (odporne na symulator i prawdziwe ECU)
    yaw_col = find_col(['z angle rate', 'angular rate, z', 'yaw'])
    lat_acc_col = find_col(['y acceleration', 'acceleration, y', 'lateral', 'ay'])

    if not yaw_col or not lat_acc_col:
        return {"error": "Nie znaleziono kolumn z YawRate (obrót Z) lub LateralAcc (przyspieszenie Y) w pliku."}

    # 2. Pobranie danych jako szybkie macierze NumPy i usunięcie NaN
    yaw_rate = pd.to_numeric(df[yaw_col], errors='coerce').fillna(0).to_numpy()
    lat_acc = pd.to_numeric(df[lat_acc_col], errors='coerce').fillna(0).to_numpy()
    
    # 3. Odpalenie matematyki: Wygładzanie sygnału filtrem Savitzky-Golay
    window = config.get("sg_frame", 21)
    poly = config.get("sg_poly", 2)
    lat_acc_filtered = kinematics.apply_savgol_filter(lat_acc, window_length=window, polyorder=poly)
    
    # 4. Agregacja (Uproszczona logika z MATLABa)
    thr_yaw = config.get("thrYaw_degps", 15.0)
    
    # Tworzymy "maskę" - tablicę Prawda/Fałsz, gdzie bolid jest w mocnym zakręcie
    in_loop_mask = np.abs(yaw_rate) > thr_yaw
    
    # Obliczamy metryki tylko dla tych momentów, gdy bolid faktycznie kręcił kółka
    if np.any(in_loop_mask):
        avg_lat_acc = np.mean(np.abs(lat_acc_filtered[in_loop_mask]))
    else:
        avg_lat_acc = 0.0

    max_lat_acc = np.max(np.abs(lat_acc_filtered))

    # 5. Zwrot gotowej paczki danych dla C#
    return {
        "status": "success",
        "competition": "Skidpad",
        "metrics": {
            "avg_lat_accel_g": round(float(avg_lat_acc), 3),
            "max_lat_accel_g": round(float(max_lat_acc), 3),
            "data_points_analyzed": len(df),
            "info": "Przetworzono z użyciem filtra Savitzky'ego-Golaya"
        }
    }