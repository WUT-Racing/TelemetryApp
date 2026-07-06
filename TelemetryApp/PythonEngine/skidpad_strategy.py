import pandas as pd
import numpy as np
import kinematics
import track_geometry

def process(df: pd.DataFrame, config: dict) -> dict:
    """
    Główna funkcja spinająca logikę Skidpada.
    Przyjmuje DataFrame z danymi i zwraca gotowe metryki jako słownik (docelowo dla C#).
    """
    # 1. Automatyczne znalezienie odpowiednich kolumn w Twoim pliku CSV
    # (Szukamy po fragmentach nazw, żeby uniezależnić się od długich nagłówków z adresami hex)
    yaw_col = next((col for col in df.columns if 'YawRate' in col or 'yaw' in col.lower()), None)
    lat_acc_col = next((col for col in df.columns if 'LateralAcc' in col or 'ay' in col.lower()), None)

    if not yaw_col or not lat_acc_col:
        return {"error": "Nie znaleziono kolumn z YawRate lub LateralAcc w pliku."}

    # 2. Pobranie danych jako szybkie macierze NumPy i pozbycie się "pustych" wartości (NaN)
    yaw_rate = pd.to_numeric(df[yaw_col], errors='coerce').fillna(0).to_numpy()
    lat_acc = pd.to_numeric(df[lat_acc_col], errors='coerce').fillna(0).to_numpy()
    
    # 3. Odpalenie matematyki: Wygładzanie sygnału filtrem (korzystamy z kinematics.py)
    window = config.get("sg_frame", 21)
    poly = config.get("sg_poly", 2)
    lat_acc_filtered = kinematics.apply_savgol_filter(lat_acc, window_length=window, polyorder=poly)
    
    # 4. Agregacja (Uproszczona logika z MATLABa)
    # Bolid robi pętlę na Skidpadzie, gdy prędkość obrotu wokół własnej osi (YawRate) jest wysoka.
    thr_yaw = config.get("thrYaw_degps", 15.0)
    
    # Tworzymy "maskę" - tablicę Prawda/Fałsz, gdzie bolid jest w mocnym zakręcie
    in_loop_mask = np.abs(yaw_rate) > thr_yaw
    
    # Obliczamy metryki tylko dla tych momentów, gdy bolid faktycznie jechał w kółko
    if np.any(in_loop_mask):
        avg_lat_acc = np.mean(np.abs(lat_acc_filtered[in_loop_mask]))
    else:
        avg_lat_acc = 0.0

    max_lat_acc = np.max(np.abs(lat_acc_filtered))

    # 5. Zwrot gotowej paczki danych (która poleci prosto do Twojej apki w C#)
    return {
        "status": "success",
        "competition": "Skidpad",
        "metrics": {
            "avg_lat_accel": round(float(avg_lat_acc), 3),
            "max_lat_accel": round(float(max_lat_acc), 3),
            "data_points_analyzed": len(df),
            "info": "Przetworzono przy użyciu modułów NumPy i SciPy"
        }
    }