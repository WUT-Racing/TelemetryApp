import json
import pandas as pd
import skidpad_strategy
import event_detector

# Zauważ nowy argument: progress_callback
def process_session(file_path: str, competition_type: str, config_json: str, progress_callback=None) -> str:
    
    # Pomocnicza funkcja do bezpiecznego wysyłania wiadomości do C#
    def log(msg):
        if progress_callback:
            progress_callback(msg)

    config = json.loads(config_json)
    
    log(f"Rozpoczynam analizę...\nPlik: {file_path}")
    log("Wczytywanie wielkich macierzy danych z CSV (to potrwa kilka sekund)...")
    
    try:
        df = pd.read_csv(file_path, sep=';', low_memory=False) 
    except Exception as e:
        err = f"Nie udało się wczytać pliku: {str(e)}"
        log(f"BŁĄD: {err}")
        return json.dumps({"error": err})

    log(f"Sukces! Wczytano {len(df)} klatek telemetrii. Rozpoznawanie dyscypliny...")

    # --- AUTOMATYCZNE WYKRYWANIE ---
    if competition_type == "Auto":
        detected_comp = event_detector.detect_competition(df)
        competition_type = detected_comp
        log(f"Zakończono analizę toru. Wykryta dyscyplina: [ {detected_comp.upper()} ]")
    else:
        detected_comp = competition_type
        log(f"Wymuszono manualnie analizę dla: {detected_comp}")

    results = {}

    # --- SKIEROWANIE DO ODPOWIEDNIEGO SKRYPTU ---
    log("Agregacja metryk i aplikowanie filtrów cyfrowych...")
    
    if competition_type == "Skidpad":
        results = skidpad_strategy.process(df, config)
    elif competition_type == "Acceleration":
        results = {"status": "success", "competition": "Acceleration", "message": "Moduł accel w budowie"}
    elif competition_type == "Autocross":
        results = {"status": "success", "competition": "Autocross", "message": "Moduł autoX w budowie"}
    elif competition_type == "Endurance":
        results = {"status": "success", "competition": "Endurance", "message": "Moduł endurance w budowie"}
    else:
        results = {"error": f"Nieobsługiwana konkurencja: {competition_type}"}

    if "metrics" in results:
        results["detected_event"] = detected_comp

    log("Analiza zakończona! Zwracam wyniki do interfejsu MAUI.")
    return json.dumps(results)