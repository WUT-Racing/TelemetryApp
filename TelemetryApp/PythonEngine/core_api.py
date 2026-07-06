import json
import pandas as pd
import skidpad_strategy # <-- Odkomentowaliśmy i używamy modułu!

def process_session(file_path: str, competition_type: str, config_json: str) -> str:
    # 1. Konwersja ustawień z C#
    config = json.loads(config_json)
    
    # 2. Wczytanie pliku CSV (zoptymalizowane pod duże pliki)
    try:
        df = pd.read_csv(file_path, sep=';', low_memory=False) 
    except Exception as e:
        return json.dumps({"error": f"Nie udało się wczytać pliku CSV: {str(e)}"})

    results = {}

    # 3. Skierowanie do odpowiedniego skryptu
    if competition_type == "Skidpad":
        # WYWOŁANIE PRAWDZIWEJ LOGIKI
        results = skidpad_strategy.process(df, config)
        
    elif competition_type == "Acceleration":
        results = {"status": "success", "message": "Tu będzie accel"}
    else:
        results = {"error": f"Nieobsługiwana konkurencja: {competition_type}"}

    # 4. Pakujemy z powrotem w format tekstowy JSON dla C#
    return json.dumps(results)