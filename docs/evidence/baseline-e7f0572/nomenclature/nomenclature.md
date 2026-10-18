# TG-263 nomenclature check (plan P1.8)

Snapshot `TemplateSync.Tests/TestData/TG263.2026-09-30.json` (246 records) against the AAPM TG-263 worksheet (`TG263_Nomenclature_Worksheet_20170815.xls`, SHA-256 `5ff0b9e2ebf578793f6fa8f59c2357b3feb61fdc0f87171e9103a0495d93d150`) and the in-repo TG-263 naming rules. Produced 2026-10-18T18:56:05Z by `tools/nomenclature/check_tg263_names.py`. The worksheet is not stored in this repository; only names from the snapshot, the matched worksheet spellings and FMAIDs appear here.

## Summary

| Snapshot field | Compared with | Exact | Ignoring case only | Unmatched | Empty |
| --- | --- | ---: | ---: | ---: | ---: |
| `Structure` | primary names | 96 | 3 | 147 | 0 |
| `TG_263` | primary names | 115 | 2 | 129 | 0 |
| `TG_263R` | reverse-order names | 115 | 1 | 130 | 0 |

Unmatched `Structure` names by snapshot Type: ORGAN 46, CTV 41, PTV 41, GTV 13, AVOIDANCE 2, BRACHY_SRC_APP 2, Control 1, MARKER 1. The worksheet lists only four target rows; target names are built from TG-263's pattern rules, so unmatched targets are expected.

Worksheet FMAID available for 101 of 246 records (through the `Structure` match, else the `TG_263` match). The snapshot has no FMAID column, so this is availability, not agreement.

| Rule over `Structure` | Violations | Same rule over the worksheet's primary names |
| --- | ---: | ---: |
| `length`: At most 16 characters | 7 | 0 |
| `unique`: Unique ignoring case | 2 | n/a |
| `target_prefix`: Targets start with GTV/CTV/ITV/PTV | 0 | 0 |
| `helper_prefix`: Helpers start with `z` or `_` | 0 | 0 |
| `laterality`: Laterality only as trailing `_L`/`_R` | 0 | 0 |
| `no_spaces`: No spaces | 1 | 0 |
| `charset`: Only letters, digits, `_ ^ + - . /` | 2 | 0 |

Records with at least one violation: 12. Rule definitions are under "Rule violations" below.

## Worksheet provenance

| Item | Value |
| --- | --- |
| Resource page | https://www.aapm.org/pubs/reports/RPT_263_Supplemental/ |
| URL | https://www.aapm.org/pubs/reports/RPT_263_Supplemental/TG263_Nomenclature_Worksheet_20170815.xls |
| Retrieved (UTC) | 2026-10-18T18:56:04Z |
| HTTP Last-Modified | Fri, 02 Mar 2018 16:20:06 GMT |
| Size (bytes) | 1,754,624 |
| SHA-256 | `5ff0b9e2ebf578793f6fa8f59c2357b3feb61fdc0f87171e9103a0495d93d150` |
| Read with | xlrd 2.0.1 |
| Sheets | `TG263 v20170815` |
| Tab read | `TG263 v20170815` |
| Rows (incl. header) | 718 |
| Data rows with a name | 717 |
| Headers found | `Target Type`, `Major Category`, `Minor Category`, `Anatomic Group`, `N Characters`, `TG263-Primary Name`, `TG-263-Reverse Order Name`, `Description`, `FMAID`, (blank) |

| Role | Header matched | Column | Matched on |
| --- | --- | ---: | --- |
| primary | `TG263-Primary Name` | 6 | "primary name" |
| reverse | `TG-263-Reverse Order Name` | 7 | "reverse order name" |
| target_type | `Target Type` | 1 | "target type" |
| category | `Major Category` | 2 | "major category" |
| fmaid | `FMAID` | 9 | "fmaid" |

Snapshot: `TemplateSync.Tests/TestData/TG263.2026-09-30.json`, 177,534 bytes, SHA-256 `94d0fbf4d6d7548fc5810050f04ec864c7af129766918c6511b9ffe3faa86d71`, generated 2026-09-30T21:46:50.2740828+00:00, `recordCount` 246.

## Unmatched and case-only names

### `Structure` against the worksheet primary names

Unmatched (147):

`PTV_Breast_Eval` (recw0PPvcjLPh0yC8), `PTVsb_Eval` (recxprYkn0Eijvkf7), `PTV_LN_Ax` (recUdyzSD1ullnNI7), `PTV_LN_Sclav` (recHRJWNaL3roadK1), `PTV_LN_IMN` (recUz0tFqFKHiuO2B), `GTVp` (recBj3OhNmVF8ufNu), `GTVn` (rec2rGx7zZfAPYURO), `CTV_Low` (reck8C5V7MTVeike4), `PTV_Low` (recKHdsGeajiceaty), `CTV_Mid` (recYw7hOrW9pRSSJm), `PTV_Mid` (recDSPs9gZmAJpBDY), `CTV_High` (recTIJRA3CfsF49zq), `PTV_High` (reccOjRZuCVrzq3Gi), `Lacrimal_Glnd_R` (recBbbEELCC0FhIYL), `Constrict_Musc_S` (rec0w405cHrUKoIpG), `Constrict_Musc_M` (rec5qOptqtjxnsROw), `Constrict_Musc_I` (recwGuP4Vbevbop3j), `Oral_Cavity` (recpnanQ95t78VRez), `Lacrimal_Glnd_L` (reczAC2WhlqFmXp7p), `Nerve` (rech9bJDVISHGpryd), `Submand_Glnd_L` (recADgjpXUsAJPFbc), `Submand_Glnd_R` (recCs5T1MvckwDnog), `Thyroid_Glnd` (recs3xr2rXt70kl8s), `Esophagus_Cervical` (rec1e2L3JXtc0hQRf), `CTV_Breast` (recQMU0ZTkYVrjfY2), `CTV_ChestWall` (recAdbrjxTO38YVqa), `CTV_LN_Ax` (recjpspL74Mnhpgs4), `CTV_LN_IMN` (recGFp9zi9iYwoP8B), `CTV_LN_Sclav` (recceQFLfUhdcOZo2), `PTV_Breast` (rec5lYbS80UD9A4Iu), `PTV_ChestWall` (recjALzTBwQllhyFo), `PTVsb` (recD0XrAqMd3RjzgI), `PTV_ChestWall_Eval` (recLRVlKPGNMJs9JU), `CTV_Lumpectomy` (recyHd2qWXGRZrTGR), `GTV_Lumpectomy` (rec3ESdKXNmHfHbmj), `CTV_Scar` (rec4akFlg4gJiQ8RJ), `PTV_Scar` (recYjR8LoRElVhfoj), `PTV_Scar_Eval` (recAjyNlsuhyrJNyk), `Ribs` (recvBlheWJQEvhBYf), `Constrict_Musc` (recm1sqAwtmanJ6dz), `Genitalia` (recOgTA70VUtPTNAU), `Bowel_Bag` (reckBGPkVm7As5gfP), `Gluteal_Folds` (recG0qHldgFzTe8Lg), `IliacCrest` (recDkuztGJno1yVaW), `Testes` (recs7nrZMueHHcCJu), `zPTV_High` (recYxFEVHQZdzgIQN), `zPTV_Mid` (rec4NxqFGyAjvyjv7), `Seminal_Ves` (recamID9ehRIIJbE0), `Pubic_Bone` (recJWrEVhXDfsZbva), `CTVn` (recDhk6616e2f3mW7), `CTVp` (rec273ZqAe6fYtJY6), `CTV_HighˆLN_3cm+` (recJZldtsfqN8lsnT), `CTV_MidˆLN_3cm-` (recpFJxxhPIGJs2YN), `_TS_CTV_High` (recbfk1LDOElql5rd), `_TS_CTV_Mid` (recKsKyvZtZpy9KoF), `_TS_CTV_Low` (rec2O0GKCFzC2t1tM), `_TS_GTVn` (recqIS3ZP2rwPLLiS), `_TS_CTV` (recJKqM3vPL0AFU1z), `_TS_PTV_High` (reclPe5ozP1z2NtZ8), `_TS_PTV_Mid` (recbRgUm3CyyYOKcS), `_TS_PTV_Low` (recwj4Mfdlfe6xOEG), `_TS_GTV` (recyJHgE3shRBojdp), `_TS_CTVsb` (recVu5JdL6BYCPCGc), `_TS_CTV_LN_IMN` (recRVrrNvIi3waKC9), `_TS_CTV_LN_Ax` (recP5TWi2JtWhNcAn), `_TS_CTV_CW` (recfVzpYT9M5VQa8g), `_TS_CTV_Breast` (rec7xrIfvkaCwSGKG), `_TS_CTV_LN_Sclav` (rec5ZInr0YEQNw6hM), `_TS_CTV_Scar` (rec1gaovzmuawHRpt), `_TS_PTV_Breast` (reccUr705f6MroD0k), `_TS_PTV_ChestWall` (recxyaE2Ws7Ge0cIq), `_TS_PTV_LN_Ax` (recisoUvA0FwvDR4U), `_TS_PTV_LN_IMN` (recJAh5n0Ig80KWnR), `_TS_PTV_LN_Sclav` (reccmK50zeaEVbY2j), `_TS_PTVsb` (recJYPYPdApYItPZ6), `_TS_PTV_Scar` (reciiEpv0B5X6FFXq), `_TS_PTV_Breast_Eval` (recVYwjNb6h1wFjdx), `_TS_PTV_CW_Eval` (recsOXEFMhXlSMC8K), `_TS_PTVsb_Eval` (rec9zSL3EM0AWSyDv), `_TS_PTV_Scar_Eval` (rec9YeL2pJxOd6cyJ), `_TS_ITV` (recYA4mSkuWbjbWrb), `Anal_Canal` (recRpyxHycZdE1gBP), `Fid` (recAYjEvLSanLVbVY), `Spacer` (recfQn4I45q4frguW), `Skin05` (recs8c1RTI37AKMtw), `Mandible_Bone` (recj60ERfl0gRwx6F), `Testis_NAdj` (recgL9cxHRTYz5IBf), `CTV1^GTV/Cervix/Uterus` (recoyrcDflVX1NEyL), `CTV2^Parametria/Vagina` (recyx3l3lVHQXNIXW), `TM_Joint` (reckSMFhMUz1tHqPx), `Temporal_Lobe_L` (recmiNPQsRFFMR0O2), `Temporal_Lobe_R` (rec5kwpaD4O7uJC6k), `Joint_Spc` (reciWPjWUSFwtwWDP), `Hi HU` (rece9WNfeuVDuw9ib), `GTVsb` (recNADTZOwimZ35kd), `CTVsb` (recUoeSPJFYaQyQBR), `_TS_PTV` (rec2JbR83EOfyMp3U), `_TS_GTVp` (rech6XWgeJxWgOCPy), `_TS_GTVsb` (recSpZHzfVyMzWym2), `_TS_CTVn` (recAe5j0WXAwHkkZr), `_TS_CTVp` (recIsfMPsHNM4NhBw), `_TS_CTVsb` (recBAais5daEWac4n), `CTV_Mid01` (rec2ZIW8LMU4nPGYR), `CTV_Mid02` (recJk6Lc5eGE2AHq1), `PTV_Mid01` (recmrqHUats1hniIz), `PTV_Mid02` (recZK6TUxKj6D8B6P), `CTV_ev12` (recfjOxYlBMnyAaIN), `PTV_Surf` (recscr3BP3ZVE9vhS), `zPTVopt` (recvdR9nKp0jchjTw), `PTV_Eval` (recBQZYydhUesPUul), `Appl_evXX` (recQUTvuPbOsGSscY), `Breast_R-PTV` (recF4dhtC2zWvDkfi), `Breast_R-CTV` (recUF4MFWiqrdgVzk), `Breast_L-PTV` (recBkY9oYGvuF0UlJ), `Breast_L-CTV` (rec1ADYzRNOCKjQSb), `Prostate_evXX` (recNq0aBdHoBN3D6m), `CTV1` (rec3qFyu0ttTMnGiU), `CTV2` (recb3kqfG7jlAEcWw), `GTV1` (recug3voafkIxnk45), `GTV2` (recjKSJiZjsAxNblE), `CTV_Eval` (recsJ4dS7KzxuRrif), `PTV_ev10` (recrLlSnNV0d2ykNi), `Skin03` (rec6FK2SAz3RToHYB), `Appl_Surf` (recNzTYHqiu0Qm1Yu), `PTV_Boost` (reccgjDBF5cUz6sUW), `OpticDisc_L` (recxuv0R42c95N879), `OpticDisc_R` (recAfmkVBO289LL0E), `Choroid` (recgCKR9NnyZvNQIb), `Apex` (recOeyXj66KvEhiju), `Sclera_Inner` (recT7zAoyJBR3HXVO), `Sclera_Outer` (rec73PrIbVFrbphye), `PTV_XXXX` (recE7cGqHwmnctZAf), `Eye_Origin_R` (recSSBFQrw5MlLjdx), `Eye_Origin_L` (recRFZpTifBAtACFU), `Fovea_L` (recYncs2k0vQL9hSD), `Fovea_R` (recnZRPn6BDAc2qsa), `GTV_LR` (rec4wER6CAFjbgsa8), `GTV_IR` (recqOCJEZDXb5PnXr), `GTV_HR` (recI0UlgLIAeBNYQQ), `CTV_LR` (recaiQ28bUPLKCxlb), `CTV_IR` (recNwKx5QzZ5aHPJs), `CTV_HR` (recFkJqfURpw7Seh5), `PTV_LR` (recSmZyTXudK4XhME), `PTV_IR` (recAb2twRT0zAqrVH), `PTV_HR` (recoqeZTyWWS0mKnX), `SkinXX` (recEzffrVpUjDmr5K), `SkinXX_PRVXX` (recy9f5hBfw1QhB44)

Matched ignoring case only (3):

`SpinalCord_PRVXX` -> `SpinalCord_PRVxx` (recfvpvXWhDmjbMB6), `BODY` -> `Body` (rec63trQNCuo2ZZ4N), `GallBladder` -> `Gallbladder` (recGsd45zxLeS70LD)

### `TG_263` against the worksheet primary names

Unmatched (129):

`PTV_Breast_Eval` (recw0PPvcjLPh0yC8), `PTVsb_Eval` (recxprYkn0Eijvkf7), `PTV_LN_Ax` (recUdyzSD1ullnNI7), `PTV_LN_Sclav` (recHRJWNaL3roadK1), `PTV_LN_IMN` (recUz0tFqFKHiuO2B), `GTVp` (recBj3OhNmVF8ufNu), `GTVn` (rec2rGx7zZfAPYURO), `CTV_Low` (reck8C5V7MTVeike4), `PTV_Low` (recKHdsGeajiceaty), `CTV_Mid` (recYw7hOrW9pRSSJm), `PTV_Mid` (recDSPs9gZmAJpBDY), `CTV_High` (recTIJRA3CfsF49zq), `PTV_High` (reccOjRZuCVrzq3Gi), `Nerve` (rech9bJDVISHGpryd), `CTV_Breast` (recQMU0ZTkYVrjfY2), `CTV_ChestWall` (recAdbrjxTO38YVqa), `CTV_LN_Ax` (recjpspL74Mnhpgs4), `CTV_LN_IMN` (recGFp9zi9iYwoP8B), `CTV_LN_Sclav` (recceQFLfUhdcOZo2), `PTV_Breast` (rec5lYbS80UD9A4Iu), `PTV_ChestWall` (recjALzTBwQllhyFo), `PTVsb` (recD0XrAqMd3RjzgI), `PTV_ChestWall_Eval` (recLRVlKPGNMJs9JU), `CTV_Lumpectomy` (recyHd2qWXGRZrTGR), `GTV_Lumpectomy` (rec3ESdKXNmHfHbmj), `CTV_Scar` (rec4akFlg4gJiQ8RJ), `PTV_Scar` (recYjR8LoRElVhfoj), `PTV_Scar_Eval` (recAjyNlsuhyrJNyk), `Ribs` (recvBlheWJQEvhBYf), `Gluteal_Folds` (recG0qHldgFzTe8Lg), `IliacCrest` (recDkuztGJno1yVaW), `Testes` (recs7nrZMueHHcCJu), `zPTV_High` (recYxFEVHQZdzgIQN), `zPTV_Mid` (rec4NxqFGyAjvyjv7), `Pubic_Bone` (recJWrEVhXDfsZbva), `CTVn` (recDhk6616e2f3mW7), `CTVp` (rec273ZqAe6fYtJY6), `CTV_HighˆLN_3cm+` (recJZldtsfqN8lsnT), `CTV_MidˆLN_3cm-` (recpFJxxhPIGJs2YN), `_TS_CTV_High` (recbfk1LDOElql5rd), `_TS_CTV_Mid` (recKsKyvZtZpy9KoF), `_TS_CTV_Low` (rec2O0GKCFzC2t1tM), `_TS_GTVn` (recqIS3ZP2rwPLLiS), `_TS_CTV` (recJKqM3vPL0AFU1z), `_TS_PTV_High` (reclPe5ozP1z2NtZ8), `_TS_PTV_Mid` (recbRgUm3CyyYOKcS), `_TS_PTV_Low` (recwj4Mfdlfe6xOEG), `_TS_GTV` (recyJHgE3shRBojdp), `_TS_CTVsb` (recVu5JdL6BYCPCGc), `_TS_CTV_LN_IMN` (recRVrrNvIi3waKC9), `_TS_CTV_LN_Ax` (recP5TWi2JtWhNcAn), `_TS_CTV_CW` (recfVzpYT9M5VQa8g), `_TS_CTV_Breast` (rec7xrIfvkaCwSGKG), `_TS_CTV_LN_Sclav` (rec5ZInr0YEQNw6hM), `_TS_CTV_Scar` (rec1gaovzmuawHRpt), `_TS_PTV_Breast` (reccUr705f6MroD0k), `_TS_PTV_ChestWall` (recxyaE2Ws7Ge0cIq), `_TS_PTV_LN_Ax` (recisoUvA0FwvDR4U), `_TS_PTV_LN_IMN` (recJAh5n0Ig80KWnR), `_TS_PTV_LN_Sclav` (reccmK50zeaEVbY2j), `_TS_PTVsb` (recJYPYPdApYItPZ6), `_TS_PTV_Scar` (reciiEpv0B5X6FFXq), `_TS_PTV_Breast_Eval` (recVYwjNb6h1wFjdx), `_TS_PTV_CW_Eval` (recsOXEFMhXlSMC8K), `_TS_PTVsb_Eval` (rec9zSL3EM0AWSyDv), `_TS_PTV_Scar_Eval` (rec9YeL2pJxOd6cyJ), `_TS_ITV` (recYA4mSkuWbjbWrb), `Fid` (recAYjEvLSanLVbVY), `Spacer` (recfQn4I45q4frguW), `IED_Pacemaker` (recJdIABkCbpzLXM9), `Skin05` (recs8c1RTI37AKMtw), `Testes_NAdj` (recgL9cxHRTYz5IBf), `CTV1^GTV/Cervix/Uterus` (recoyrcDflVX1NEyL), `CTV2^Parametria/Vagina` (recyx3l3lVHQXNIXW), `Spc_Joint` (reciWPjWUSFwtwWDP), `Hi HU` (rece9WNfeuVDuw9ib), `GTVsb` (recNADTZOwimZ35kd), `CTVsb` (recUoeSPJFYaQyQBR), `_TS_PTV` (rec2JbR83EOfyMp3U), `_TS_GTVp` (rech6XWgeJxWgOCPy), `_TS_GTVsb` (recSpZHzfVyMzWym2), `_TS_CTVn` (recAe5j0WXAwHkkZr), `_TS_CTVp` (recIsfMPsHNM4NhBw), `_TS_CTVsb` (recBAais5daEWac4n), `CTV_Mid01` (rec2ZIW8LMU4nPGYR), `CTV_Mid02` (recJk6Lc5eGE2AHq1), `PTV_Mid01` (recmrqHUats1hniIz), `PTV_Mid02` (recZK6TUxKj6D8B6P), `CTV_ev12` (recfjOxYlBMnyAaIN), `PTV_Surf` (recscr3BP3ZVE9vhS), `zPTVopt` (recvdR9nKp0jchjTw), `PTV_Eval` (recBQZYydhUesPUul), `Appl_evXX` (recQUTvuPbOsGSscY), `Breast_R-PTV` (recF4dhtC2zWvDkfi), `Breast_R-CTV` (recUF4MFWiqrdgVzk), `Breast_L-PTV` (recBkY9oYGvuF0UlJ), `Breast_L-CTV` (rec1ADYzRNOCKjQSb), `Prostate_evXX` (recNq0aBdHoBN3D6m), `CTV1` (rec3qFyu0ttTMnGiU), `CTV2` (recb3kqfG7jlAEcWw), `GTV1` (recug3voafkIxnk45), `GTV2` (recjKSJiZjsAxNblE), `CTV_Eval` (recsJ4dS7KzxuRrif), `PTV_ev10` (recrLlSnNV0d2ykNi), `Skin03` (rec6FK2SAz3RToHYB), `Appl_Surf` (recNzTYHqiu0Qm1Yu), `PTV_Boost` (reccgjDBF5cUz6sUW), `OpticDisc_L` (recxuv0R42c95N879), `OpticDisc_R` (recAfmkVBO289LL0E), `Choroid` (recgCKR9NnyZvNQIb), `Apex` (recOeyXj66KvEhiju), `Sclera_Inner` (recT7zAoyJBR3HXVO), `Sclera_Outer` (rec73PrIbVFrbphye), `PTV_XXXX` (recE7cGqHwmnctZAf), `Eye_Origin_R` (recSSBFQrw5MlLjdx), `Eye_Origin_L` (recRFZpTifBAtACFU), `Fovea_L` (recYncs2k0vQL9hSD), `Fovea_R` (recnZRPn6BDAc2qsa), `GTV_LR` (rec4wER6CAFjbgsa8), `GTV_IR` (recqOCJEZDXb5PnXr), `GTV_HR` (recI0UlgLIAeBNYQQ), `CTV_LR` (recaiQ28bUPLKCxlb), `CTV_IR` (recNwKx5QzZ5aHPJs), `CTV_HR` (recFkJqfURpw7Seh5), `PTV_LR` (recSmZyTXudK4XhME), `PTV_IR` (recAb2twRT0zAqrVH), `PTV_HR` (recoqeZTyWWS0mKnX), `SkinXX` (recEzffrVpUjDmr5K), `SkinXX_PRVXX` (recy9f5hBfw1QhB44)

Matched ignoring case only (2):

`SpinalCord_PRVXX` -> `SpinalCord_PRVxx` (recfvpvXWhDmjbMB6), `BODY` -> `Body` (rec63trQNCuo2ZZ4N)

### `TG_263R` against the worksheet reverse-order names

Unmatched (130):

`PTV_Breast_Eval` (recw0PPvcjLPh0yC8), `PTVsb_Eval` (recxprYkn0Eijvkf7), `PTV_LN_Ax` (recUdyzSD1ullnNI7), `PTV_LN_Sclav` (recHRJWNaL3roadK1), `PTV_LN_IMN` (recUz0tFqFKHiuO2B), `GTVp` (recBj3OhNmVF8ufNu), `GTVn` (rec2rGx7zZfAPYURO), `CTV_Low` (reck8C5V7MTVeike4), `PTV_Low` (recKHdsGeajiceaty), `CTV_Mid` (recYw7hOrW9pRSSJm), `PTV_Mid` (recDSPs9gZmAJpBDY), `CTV_High` (recTIJRA3CfsF49zq), `PTV_High` (reccOjRZuCVrzq3Gi), `Nerve` (rech9bJDVISHGpryd), `SpinalCord_PRVXX` (recfvpvXWhDmjbMB6), `CTV_Breast` (recQMU0ZTkYVrjfY2), `CTV_ChestWall` (recAdbrjxTO38YVqa), `CTV_LN_Ax` (recjpspL74Mnhpgs4), `CTV_LN_IMN` (recGFp9zi9iYwoP8B), `CTV_LN_Sclav` (recceQFLfUhdcOZo2), `PTV_Breast` (rec5lYbS80UD9A4Iu), `PTV_ChestWall` (recjALzTBwQllhyFo), `PTVsb` (recD0XrAqMd3RjzgI), `PTV_ChestWall_Eval` (recLRVlKPGNMJs9JU), `CTV_Lumpectomy` (recyHd2qWXGRZrTGR), `GTV_Lumpectomy` (rec3ESdKXNmHfHbmj), `CTV_Scar` (rec4akFlg4gJiQ8RJ), `PTV_Scar` (recYjR8LoRElVhfoj), `PTV_Scar_Eval` (recAjyNlsuhyrJNyk), `Ribs` (recvBlheWJQEvhBYf), `Folds_Gluteal` (recG0qHldgFzTe8Lg), `IliacCrest` (recDkuztGJno1yVaW), `Testes` (recs7nrZMueHHcCJu), `zPTV_High` (recYxFEVHQZdzgIQN), `zPTV_Mid` (rec4NxqFGyAjvyjv7), `Bone_Pubic` (recJWrEVhXDfsZbva), `CTVn` (recDhk6616e2f3mW7), `CTVp` (rec273ZqAe6fYtJY6), `CTV_HighˆLN_3cm+` (recJZldtsfqN8lsnT), `CTV_MidˆLN_3cm-` (recpFJxxhPIGJs2YN), `_TS_CTV_High` (recbfk1LDOElql5rd), `_TS_CTV_Mid` (recKsKyvZtZpy9KoF), `_TS_CTV_Low` (rec2O0GKCFzC2t1tM), `_TS_GTVn` (recqIS3ZP2rwPLLiS), `_TS_CTV` (recJKqM3vPL0AFU1z), `_TS_PTV_High` (reclPe5ozP1z2NtZ8), `_TS_PTV_Mid` (recbRgUm3CyyYOKcS), `_TS_PTV_Low` (recwj4Mfdlfe6xOEG), `_TS_GTV` (recyJHgE3shRBojdp), `_TS_CTVsb` (recVu5JdL6BYCPCGc), `_TS_CTV_LN_IMN` (recRVrrNvIi3waKC9), `_TS_CTV_LN_Ax` (recP5TWi2JtWhNcAn), `_TS_CTV_CW` (recfVzpYT9M5VQa8g), `_TS_CTV_Breast` (rec7xrIfvkaCwSGKG), `_TS_CTV_LN_Sclav` (rec5ZInr0YEQNw6hM), `_TS_CTV_Scar` (rec1gaovzmuawHRpt), `_TS_PTV_Breast` (reccUr705f6MroD0k), `_TS_PTV_ChestWall` (recxyaE2Ws7Ge0cIq), `_TS_PTV_LN_Ax` (recisoUvA0FwvDR4U), `_TS_PTV_LN_IMN` (recJAh5n0Ig80KWnR), `_TS_PTV_LN_Sclav` (reccmK50zeaEVbY2j), `_TS_PTVsb` (recJYPYPdApYItPZ6), `_TS_PTV_Scar` (reciiEpv0B5X6FFXq), `_TS_PTV_Breast_Eval` (recVYwjNb6h1wFjdx), `_TS_PTV_CW_Eval` (recsOXEFMhXlSMC8K), `_TS_PTVsb_Eval` (rec9zSL3EM0AWSyDv), `_TS_PTV_Scar_Eval` (rec9YeL2pJxOd6cyJ), `_TS_ITV` (recYA4mSkuWbjbWrb), `Fid` (recAYjEvLSanLVbVY), `Spacer` (recfQn4I45q4frguW), `Pacemaker_IED` (recJdIABkCbpzLXM9), `Skin05` (recs8c1RTI37AKMtw), `NAdj_Testes` (recgL9cxHRTYz5IBf), `CTV1^GTV/Cervix/Uterus` (recoyrcDflVX1NEyL), `CTV2^Parametria/Vagina` (recyx3l3lVHQXNIXW), `Joint_Spc` (reciWPjWUSFwtwWDP), `Hi HU` (rece9WNfeuVDuw9ib), `GTVsb` (recNADTZOwimZ35kd), `CTVsb` (recUoeSPJFYaQyQBR), `_TS_PTV` (rec2JbR83EOfyMp3U), `_TS_GTVp` (rech6XWgeJxWgOCPy), `_TS_GTVsb` (recSpZHzfVyMzWym2), `_TS_CTVn` (recAe5j0WXAwHkkZr), `_TS_CTVp` (recIsfMPsHNM4NhBw), `_TS_CTVsb` (recBAais5daEWac4n), `CTV_Mid01` (rec2ZIW8LMU4nPGYR), `CTV_Mid02` (recJk6Lc5eGE2AHq1), `PTV_Mid01` (recmrqHUats1hniIz), `PTV_Mid02` (recZK6TUxKj6D8B6P), `CTV_ev12` (recfjOxYlBMnyAaIN), `PTV_Surf` (recscr3BP3ZVE9vhS), `zPTVopt` (recvdR9nKp0jchjTw), `PTV_Eval` (recBQZYydhUesPUul), `Appl_evXX` (recQUTvuPbOsGSscY), `R_Breast-PTV` (recF4dhtC2zWvDkfi), `R_Breast-CTV` (recUF4MFWiqrdgVzk), `L_Breast-PTV` (recBkY9oYGvuF0UlJ), `L_Breast-CTV` (rec1ADYzRNOCKjQSb), `Prostate_evXX` (recNq0aBdHoBN3D6m), `CTV1` (rec3qFyu0ttTMnGiU), `CTV2` (recb3kqfG7jlAEcWw), `GTV1` (recug3voafkIxnk45), `GTV2` (recjKSJiZjsAxNblE), `CTV_Eval` (recsJ4dS7KzxuRrif), `PTV_ev10` (recrLlSnNV0d2ykNi), `Skin03` (rec6FK2SAz3RToHYB), `Appl_Surf` (recNzTYHqiu0Qm1Yu), `PTV_Boost` (reccgjDBF5cUz6sUW), `L_OpticDisc` (recxuv0R42c95N879), `R_OpticDisc` (recAfmkVBO289LL0E), `Choroid` (recgCKR9NnyZvNQIb), `Apex` (recOeyXj66KvEhiju), `Inner_Sclera` (recT7zAoyJBR3HXVO), `Outer_Sclera` (rec73PrIbVFrbphye), `PTV_XXXX` (recE7cGqHwmnctZAf), `R_Origin_Eye` (recSSBFQrw5MlLjdx), `L_Origin_Eye` (recRFZpTifBAtACFU), `L_Fovea` (recYncs2k0vQL9hSD), `R_Fovea` (recnZRPn6BDAc2qsa), `GTV_LR` (rec4wER6CAFjbgsa8), `GTV_IR` (recqOCJEZDXb5PnXr), `GTV_HR` (recI0UlgLIAeBNYQQ), `CTV_LR` (recaiQ28bUPLKCxlb), `CTV_IR` (recNwKx5QzZ5aHPJs), `CTV_HR` (recFkJqfURpw7Seh5), `PTV_LR` (recSmZyTXudK4XhME), `PTV_IR` (recAb2twRT0zAqrVH), `PTV_HR` (recoqeZTyWWS0mKnX), `SkinXX` (recEzffrVpUjDmr5K), `SkinXX_PRVXX` (recy9f5hBfw1QhB44)

Matched ignoring case only (1):

`BODY` -> `Body` (rec63trQNCuo2ZZ4N)

## Rule violations

### `length` (7)

At most 16 characters (Unicode code points).

| Record id | Structure | Detail |
| --- | --- | --- |
| rec1e2L3JXtc0hQRf | `Esophagus_Cervical` | 18 characters |
| recLRVlKPGNMJs9JU | `PTV_ChestWall_Eval` | 18 characters |
| recxyaE2Ws7Ge0cIq | `_TS_PTV_ChestWall` | 17 characters |
| recVYwjNb6h1wFjdx | `_TS_PTV_Breast_Eval` | 19 characters |
| rec9YeL2pJxOd6cyJ | `_TS_PTV_Scar_Eval` | 17 characters |
| recoyrcDflVX1NEyL | `CTV1^GTV/Cervix/Uterus` | 22 characters |
| recyx3l3lVHQXNIXW | `CTV2^Parametria/Vagina` | 22 characters |

### `unique` (2)

Unique ignoring case across the list (each record of a colliding group is listed).

| Record id | Structure | Detail |
| --- | --- | --- |
| recVu5JdL6BYCPCGc | `_TS_CTVsb` | 2 records: recVu5JdL6BYCPCGc, recBAais5daEWac4n |
| recBAais5daEWac4n | `_TS_CTVsb` | 2 records: recVu5JdL6BYCPCGc, recBAais5daEWac4n |

### `target_prefix` (0)

Target names start with GTV, CTV, ITV or PTV. A record is a target when its snapshot Type is GTV, CTV, ITV or PTV. A target whose name starts with a helper prefix (`z` or `_`) is a helper, not checked here, and listed separately.

None.

### `helper_prefix` (0)

Helper or non-evaluation structures start with `z` or `_`. A record is a helper when its snapshot Type is CONTROL (any case) or its name has a helper word (opt, ring, avoid, ctrl, control, dummy, temp, help, helper) not followed by a letter; this is a heuristic, so the helpers found are listed.

None.

### `laterality` (0)

Laterality only as a trailing `_L` or `_R`: no `L`/`R` token before the last one, and no `Lt`, `Rt`, `Left`, `Right` or lower-case `l`/`r` token anywhere (tokens are the parts between `_`).

None.

### `no_spaces` (1)

No spaces (or other whitespace).

| Record id | Structure | Detail |
| --- | --- | --- |
| rece9WNfeuVDuw9ib | `Hi HU` | 1 whitespace character(s) |

### `charset` (2)

Only letters A-Z/a-z, digits, `_`, `^`, `+`, `-`, `.` and `/`.

| Record id | Structure | Detail |
| --- | --- | --- |
| recJZldtsfqN8lsnT | `CTV_HighˆLN_3cm+` | U+02C6 'ˆ' |
| recpFJxxhPIGJs2YN | `CTV_MidˆLN_3cm-` | U+02C6 'ˆ' |

Records treated as helpers by the `helper_prefix` rule (1): `zPTVopt` (Type Control).

Targets exempt from the `target_prefix` rule because their name starts with a helper prefix (36): `zPTV_High` (Type PTV), `zPTV_Mid` (Type PTV), `_TS_CTV_High` (Type CTV), `_TS_CTV_Mid` (Type CTV), `_TS_CTV_Low` (Type CTV), `_TS_GTVn` (Type GTV), `_TS_CTV` (Type CTV), `_TS_PTV_High` (Type PTV), `_TS_PTV_Mid` (Type PTV), `_TS_PTV_Low` (Type PTV), `_TS_GTV` (Type GTV), `_TS_CTVsb` (Type CTV), `_TS_CTV_LN_IMN` (Type CTV), `_TS_CTV_LN_Ax` (Type CTV), `_TS_CTV_CW` (Type CTV), `_TS_CTV_Breast` (Type CTV), `_TS_CTV_LN_Sclav` (Type CTV), `_TS_CTV_Scar` (Type CTV), `_TS_PTV_Breast` (Type PTV), `_TS_PTV_ChestWall` (Type PTV), `_TS_PTV_LN_Ax` (Type PTV), `_TS_PTV_LN_IMN` (Type PTV), `_TS_PTV_LN_Sclav` (Type PTV), `_TS_PTVsb` (Type PTV), `_TS_PTV_Scar` (Type PTV), `_TS_PTV_Breast_Eval` (Type PTV), `_TS_PTV_CW_Eval` (Type PTV), `_TS_PTVsb_Eval` (Type PTV), `_TS_PTV_Scar_Eval` (Type PTV), `_TS_ITV` (Type CTV), `_TS_PTV` (Type PTV), `_TS_GTVp` (Type GTV), `_TS_GTVsb` (Type GTV), `_TS_CTVn` (Type CTV), `_TS_CTVp` (Type CTV), `_TS_CTVsb` (Type CTV).

### Calibration over the worksheet's primary names

The per-name rules run over the 717 worksheet primary names (worksheet target type `Target` counts as a target). These are TG-263's own names, so a hit here shows where a rule as implemented is stricter than the report itself.

- `length`: 0
- `target_prefix`: 0
- `helper_prefix`: 0
- `laterality`: 0
- `no_spaces`: 0
- `charset`: 0

## Every record

Match columns: exact, case only, no, or (empty). FMAID is the worksheet's, for the matched name.

| Record id | Structure | Type | Structure match | TG_263 match | TG_263R match | Worksheet FMAID | Rules broken |
| --- | --- | --- | --- | --- | --- | --- | --- |
| recw0PPvcjLPh0yC8 | `PTV_Breast_Eval` | PTV | no | no | no |  |  |
| recxprYkn0Eijvkf7 | `PTVsb_Eval` | PTV | no | no | no |  |  |
| recUdyzSD1ullnNI7 | `PTV_LN_Ax` | PTV | no | no | no |  |  |
| recHRJWNaL3roadK1 | `PTV_LN_Sclav` | PTV | no | no | no |  |  |
| recUz0tFqFKHiuO2B | `PTV_LN_IMN` | PTV | no | no | no |  |  |
| recjEF55CpknPQrnV | `Breast_L` | ORGAN | exact | exact | exact | 321497 |  |
| recuy4hNvK6FS213V | `Lung_L` | ORGAN | exact | exact | exact | 7310 |  |
| recLpiHIuMa60QWOT | `Lung_R` | ORGAN | exact | exact | exact | 7309 |  |
| recdHfFlUgNERtUz7 | `Heart` | ORGAN | exact | exact | exact | 7088 |  |
| recMZXj0nR5t0U49k | `A_LAD` | ORGAN | exact | exact | exact | 3862 |  |
| rec8H3tohBsCfmtwt | `BrachialPlex_L` | ORGAN | exact | exact | exact | 45245 |  |
| recq751eTJoKjmGar | `BrachialPlex_R` | ORGAN | exact | exact | exact | 45244 |  |
| recT9fiJjDgucb46d | `GTV` | GTV | exact | exact | exact |  |  |
| recj1MS1kaKDtrh2d | `CTV` | CTV | exact | exact | exact |  |  |
| recBj3OhNmVF8ufNu | `GTVp` | GTV | no | no | no |  |  |
| rec2rGx7zZfAPYURO | `GTVn` | GTV | no | no | no |  |  |
| reck8C5V7MTVeike4 | `CTV_Low` | CTV | no | no | no |  |  |
| recKHdsGeajiceaty | `PTV_Low` | PTV | no | no | no |  |  |
| recYw7hOrW9pRSSJm | `CTV_Mid` | CTV | no | no | no |  |  |
| recDSPs9gZmAJpBDY | `PTV_Mid` | PTV | no | no | no |  |  |
| recTIJRA3CfsF49zq | `CTV_High` | CTV | no | no | no |  |  |
| reccOjRZuCVrzq3Gi | `PTV_High` | PTV | no | no | no |  |  |
| recj2CIUN0fns6ely | `Brain-GTV` | ORGAN | exact | exact | exact |  |  |
| recLn4uInAMjc3koB | `Cerebellum` | ORGAN | exact | exact | exact | 67944 |  |
| rec7suZZcexLfhNKj | `Cochlea_R` | ORGAN | exact | exact | exact | 60202 |  |
| rec5nVk0aQcKXC0cM | `Eye_L` | ORGAN | exact | exact | exact | 12515 |  |
| recand5cR1MJeFLN2 | `Eye_R` | ORGAN | exact | exact | exact | 12514 |  |
| receTPM5TtUC68v2j | `Hippocampi` | ORGAN | exact | exact | exact | 275020 |  |
| recKPqBQkP6EAChAP | `Hippocampus_L` | ORGAN | exact | exact | exact | 275024 |  |
| recVFIyIA5ICGNDgX | `Hippocampus_R` | ORGAN | exact | exact | exact | 275022 |  |
| recBbbEELCC0FhIYL | `Lacrimal_Glnd_R` | ORGAN | no | exact | exact | 59102 |  |
| reccW9WRmWb9qmmQC | `Lens_L` | ORGAN | exact | exact | exact | 58243 |  |
| recyLg6FCwqnFnCNa | `OpticChiasm` | ORGAN | exact | exact | exact | 62045 |  |
| recHCdvcW0Lgp4vH1 | `Orbit_L` | ORGAN | exact | exact | exact | 54668 |  |
| recDB5tTQRpbp4LEh | `Orbit_R` | ORGAN | exact | exact | exact | 54667 |  |
| recFqWa1Zfu1etng9 | `Pituitary` | ORGAN | exact | exact | exact | 13889 |  |
| rec5zgWWpvyZK6911 | `SpinalCord` | ORGAN | exact | exact | exact | 7647 |  |
| rec0w405cHrUKoIpG | `Constrict_Musc_S` | ORGAN | no | exact | exact | 46621 |  |
| rec5qOptqtjxnsROw | `Constrict_Musc_M` | ORGAN | no | exact | exact | 46622 |  |
| recwGuP4Vbevbop3j | `Constrict_Musc_I` | ORGAN | no | exact | exact | 46623 |  |
| rechjzSJtZ1Mn2XDE | `Cricopharyngeus` | ORGAN | exact | exact | exact | 46661 |  |
| recWwJnuwkjXbixar | `Cochlea_L` | ORGAN | exact | exact | exact | 60203 |  |
| recpnanQ95t78VRez | `Oral_Cavity` | ORGAN | no | exact | exact | 20292 |  |
| recYZGUEauuGurvfV | `Lips` | ORGAN | exact | exact | exact | 59815 |  |
| recAU9UFM4oGfsRn5 | `Parotid_L` | ORGAN | exact | exact | exact | 59798 |  |
| recjYh21zeffyfoJh | `Parotid_R` | ORGAN | exact | exact | exact | 59797 |  |
| recg96rl4SWl0fgXe | `Parotids` | ORGAN | exact | exact | exact | 320436 |  |
| reczAC2WhlqFmXp7p | `Lacrimal_Glnd_L` | ORGAN | no | exact | exact | 59103 |  |
| recq6wSf98iRG3yTA | `Esophagus` | ORGAN | exact | exact | exact | 7131 |  |
| rectqFweUsl4Rqj9u | `Brain` | ORGAN | exact | exact | exact | 50801 |  |
| recwQXvnwEqvfafY9 | `Brainstem` | ORGAN | exact | exact | exact | 79876 |  |
| rec2x8yX82pVB72XA | `Lens_R` | ORGAN | exact | exact | exact | 58242 |  |
| recxd07uYmNjXtsmY | `Larynx` | ORGAN | exact | exact | exact | 55097 |  |
| recKVtMgq16AfLSE7 | `OpticNrv_L` | ORGAN | exact | exact | exact | 50878 |  |
| reczXaf79H4F9yiO0 | `OpticNrv_R` | ORGAN | exact | exact | exact | 50875 |  |
| rech9bJDVISHGpryd | `Nerve` | ORGAN | no | no | no |  |  |
| recADgjpXUsAJPFbc | `Submand_Glnd_L` | ORGAN | no | exact | exact | 59803 |  |
| recCs5T1MvckwDnog | `Submand_Glnd_R` | ORGAN | no | exact | exact | 59802 |  |
| recs3xr2rXt70kl8s | `Thyroid_Glnd` | ORGAN | no | exact | exact | 9603 |  |
| recfvpvXWhDmjbMB6 | `SpinalCord_PRVXX` | ORGAN | case only | case only | no |  |  |
| rec1e2L3JXtc0hQRf | `Esophagus_Cervical` | ORGAN | no | exact | exact |  | length |
| rec63trQNCuo2ZZ4N | `BODY` | EXTERNAL | case only | case only | case only | 256135 |  |
| recQMU0ZTkYVrjfY2 | `CTV_Breast` | CTV | no | no | no |  |  |
| recAdbrjxTO38YVqa | `CTV_ChestWall` | CTV | no | no | no |  |  |
| recjpspL74Mnhpgs4 | `CTV_LN_Ax` | CTV | no | no | no |  |  |
| recGFp9zi9iYwoP8B | `CTV_LN_IMN` | CTV | no | no | no |  |  |
| recceQFLfUhdcOZo2 | `CTV_LN_Sclav` | CTV | no | no | no |  |  |
| rec5lYbS80UD9A4Iu | `PTV_Breast` | PTV | no | no | no |  |  |
| recjALzTBwQllhyFo | `PTV_ChestWall` | PTV | no | no | no |  |  |
| recD0XrAqMd3RjzgI | `PTVsb` | PTV | no | no | no |  |  |
| recLRVlKPGNMJs9JU | `PTV_ChestWall_Eval` | PTV | no | no | no |  | length |
| recyHd2qWXGRZrTGR | `CTV_Lumpectomy` | CTV | no | no | no |  |  |
| rec3ESdKXNmHfHbmj | `GTV_Lumpectomy` | GTV | no | no | no |  |  |
| recbAp72RJl2MOO5M | `Scar` | MARKER | exact | exact | exact |  |  |
| rec4akFlg4gJiQ8RJ | `CTV_Scar` | CTV | no | no | no |  |  |
| recYjR8LoRElVhfoj | `PTV_Scar` | PTV | no | no | no |  |  |
| recAjyNlsuhyrJNyk | `PTV_Scar_Eval` | PTV | no | no | no |  |  |
| recvBlheWJQEvhBYf | `Ribs` | ORGAN | no | no | no |  |  |
| recm1sqAwtmanJ6dz | `Constrict_Musc` | ORGAN | no | exact | exact | 46620 |  |
| recg5gujJ26Y2x6Du | `Stomach` | ORGAN | exact | exact | exact | 7148 |  |
| recKG6Qe7VZPmOj3U | `Spleen` | ORGAN | exact | exact | exact | 7196 |  |
| recN4tj8noppUUcYy | `Liver` | ORGAN | exact | exact | exact | 7197 |  |
| reccJ9NqsywoDwWXU | `Trachea` | ORGAN | exact | exact | exact | 7394 |  |
| rectydkr9Cv7eLy8C | `Bronchus_L` | ORGAN | exact | exact | exact | 26662 |  |
| recxmOiFftwBr9ryo | `Bronchus_R` | ORGAN | exact | exact | exact | 26661 |  |
| reciWiM5A2bYq2Gto | `Bowel_Small` | ORGAN | exact | exact | exact | 7200 |  |
| rec1PxJhNalrMwVTP | `Duodenum` | ORGAN | exact | exact | exact | 7206 |  |
| recKCW3TP9FAKy2uy | `Kidney_L` | ORGAN | exact | exact | exact | 7205 |  |
| recGtx8dyXDD51Qtx | `Kidney_R` | ORGAN | exact | exact | exact | 7204 |  |
| rec11mWlpHIwANOVl | `Kidneys` | ORGAN | exact | exact | exact | 264815 |  |
| recOHaEfJitQqdbz8 | `CaudaEquina` | ORGAN | exact | exact | exact | 52590 |  |
| rec4PkNjCmqn3Epxk | `Colon_Sigmoid` | ORGAN | exact | exact | exact | 14548 |  |
| rec4fJggpveBl3JOf | `Rectum` | ORGAN | exact | exact | exact | 14544 |  |
| recVnWmnsmCiciwN8 | `Ureter_L` | ORGAN | exact | exact | exact | 17888 |  |
| rec9MlkS6UVUW8ZTG | `Ureter_R` | ORGAN | exact | exact | exact | 17887 |  |
| recU5xJ3cwU0vCQLZ | `Bladder` | ORGAN | exact | exact | exact | 15900 |  |
| recfXwdtvcXF70Y3C | `Ovary_L` | ORGAN | exact | exact | exact | 7214 |  |
| recQfwawnFqRN0LUZ | `Ovary_R` | ORGAN | exact | exact | exact | 7213 |  |
| recOgTA70VUtPTNAU | `Genitalia` | ORGAN | no | exact | exact | 45643 |  |
| rec9nXS6ImToLC6gP | `Urethra` | ORGAN | exact | exact | exact | 19667 |  |
| recTg5zfNjtCH9wtk | `PenileBulb` | ORGAN | exact | exact | exact | 19614 |  |
| rec6d9QADVXqceL6K | `Vagina` | ORGAN | exact | exact | exact | 19949 |  |
| recLRmiyLQaPvdVSi | `BoneMarrow` | ORGAN | exact | exact | exact | 9608 |  |
| recjIpjJ0s7klMBZz | `Femur_Head_L` | ORGAN | exact | exact | exact | 32843 |  |
| reccEB12BSjHsi4Jo | `Femur_Head_R` | ORGAN | exact | exact | exact | 32842 |  |
| rece8xf1hWHcvzDA8 | `Bowel_Large` | ORGAN | exact | exact | exact | 7201 |  |
| reckBGPkVm7As5gfP | `Bowel_Bag` | ORGAN | no | exact | exact |  |  |
| recqfiqzFFU0ZzlM4 | `Femurs` | ORGAN | exact | exact | exact | 9611 |  |
| recPnzmSRLlnzZ7FW | `PTV` | PTV | exact | exact | exact |  |  |
| recG0qHldgFzTe8Lg | `Gluteal_Folds` | ORGAN | no | no | no |  |  |
| recDkuztGJno1yVaW | `IliacCrest` | ORGAN | no | no | no |  |  |
| recs7nrZMueHHcCJu | `Testes` | ORGAN | no | no | no |  |  |
| recHRE3iL6TGxsYKJ | `SacralPlex` | ORGAN | exact | exact | exact | 5909 |  |
| recYxFEVHQZdzgIQN | `zPTV_High` | PTV | no | no | no |  |  |
| rec4NxqFGyAjvyjv7 | `zPTV_Mid` | PTV | no | no | no |  |  |
| recjfC68T2Keqljkj | `Prostate` | ORGAN | exact | exact | exact | 9600 |  |
| recamID9ehRIIJbE0 | `Seminal_Ves` | ORGAN | no | exact | exact | 19387 |  |
| recJWrEVhXDfsZbva | `Pubic_Bone` | ORGAN | no | no | no |  |  |
| recDhk6616e2f3mW7 | `CTVn` | CTV | no | no | no |  |  |
| rec273ZqAe6fYtJY6 | `CTVp` | CTV | no | no | no |  |  |
| recuOyND5GA7Gueb0 | `Lungs` | ORGAN | exact | exact | exact | 68877 |  |
| rec4uM3lFyfodtgbG | `ITV` | CTV | exact | exact | exact |  |  |
| recJZldtsfqN8lsnT | `CTV_HighˆLN_3cm+` | CTV | no | no | no |  | charset |
| recpFJxxhPIGJs2YN | `CTV_MidˆLN_3cm-` | CTV | no | no | no |  | charset |
| recbfk1LDOElql5rd | `_TS_CTV_High` | CTV | no | no | no |  |  |
| recKsKyvZtZpy9KoF | `_TS_CTV_Mid` | CTV | no | no | no |  |  |
| rec2O0GKCFzC2t1tM | `_TS_CTV_Low` | CTV | no | no | no |  |  |
| recqIS3ZP2rwPLLiS | `_TS_GTVn` | GTV | no | no | no |  |  |
| recJKqM3vPL0AFU1z | `_TS_CTV` | CTV | no | no | no |  |  |
| reclPe5ozP1z2NtZ8 | `_TS_PTV_High` | PTV | no | no | no |  |  |
| recbRgUm3CyyYOKcS | `_TS_PTV_Mid` | PTV | no | no | no |  |  |
| recwj4Mfdlfe6xOEG | `_TS_PTV_Low` | PTV | no | no | no |  |  |
| recyJHgE3shRBojdp | `_TS_GTV` | GTV | no | no | no |  |  |
| recBxwor0nPjfrOVI | `Breast_R` | ORGAN | exact | exact | exact | 321496 |  |
| recVu5JdL6BYCPCGc | `_TS_CTVsb` | CTV | no | no | no |  | unique |
| recRVrrNvIi3waKC9 | `_TS_CTV_LN_IMN` | CTV | no | no | no |  |  |
| recP5TWi2JtWhNcAn | `_TS_CTV_LN_Ax` | CTV | no | no | no |  |  |
| recfVzpYT9M5VQa8g | `_TS_CTV_CW` | CTV | no | no | no |  |  |
| rec7xrIfvkaCwSGKG | `_TS_CTV_Breast` | CTV | no | no | no |  |  |
| rec5ZInr0YEQNw6hM | `_TS_CTV_LN_Sclav` | CTV | no | no | no |  |  |
| rec1gaovzmuawHRpt | `_TS_CTV_Scar` | CTV | no | no | no |  |  |
| reccUr705f6MroD0k | `_TS_PTV_Breast` | PTV | no | no | no |  |  |
| recxyaE2Ws7Ge0cIq | `_TS_PTV_ChestWall` | PTV | no | no | no |  | length |
| recisoUvA0FwvDR4U | `_TS_PTV_LN_Ax` | PTV | no | no | no |  |  |
| recJAh5n0Ig80KWnR | `_TS_PTV_LN_IMN` | PTV | no | no | no |  |  |
| reccmK50zeaEVbY2j | `_TS_PTV_LN_Sclav` | PTV | no | no | no |  |  |
| recJYPYPdApYItPZ6 | `_TS_PTVsb` | PTV | no | no | no |  |  |
| reciiEpv0B5X6FFXq | `_TS_PTV_Scar` | PTV | no | no | no |  |  |
| recVYwjNb6h1wFjdx | `_TS_PTV_Breast_Eval` | PTV | no | no | no |  | length |
| recsOXEFMhXlSMC8K | `_TS_PTV_CW_Eval` | PTV | no | no | no |  |  |
| rec9zSL3EM0AWSyDv | `_TS_PTVsb_Eval` | PTV | no | no | no |  |  |
| rec9YeL2pJxOd6cyJ | `_TS_PTV_Scar_Eval` | PTV | no | no | no |  | length |
| recYA4mSkuWbjbWrb | `_TS_ITV` | CTV | no | no | no |  |  |
| recRpyxHycZdE1gBP | `Anal_Canal` | ORGAN | no | exact | exact | 15703 |  |
| recRV33BphcXUDE5k | `A_Aorta` | ORGAN | exact | exact | exact | 3734 |  |
| recUvVocaYZZPmOVp | `Carina` | ORGAN | exact | exact | exact | 7465 |  |
| recAYjEvLSanLVbVY | `Fid` | MARKER | no | no | no |  |  |
| recoIQSKPmvaWl3II | `GreatVes` | ORGAN | exact | exact | exact |  |  |
| recfQn4I45q4frguW | `Spacer` | AVOIDANCE | no | no | no |  |  |
| recJdIABkCbpzLXM9 | `Pacemaker` | AVOIDANCE | exact | no | no |  |  |
| recs8c1RTI37AKMtw | `Skin05` | ORGAN | no | no | no |  |  |
| recAWznRMR06pjK4f | `Bladder-CTV` | ORGAN | exact | exact | exact |  |  |
| recKvTTGeyWEp2inU | `Bone` | ORGAN | exact | exact | exact | 30317 |  |
| recx9TVR8K2hGU1QQ | `Chestwall` | ORGAN | exact | exact | exact | 50060 |  |
| recFvm3GUhXBzlchG | `Lungs-GTV` | ORGAN | exact | exact | exact |  |  |
| recj60ERfl0gRwx6F | `Mandible_Bone` | ORGAN | no | exact | exact | 52748 |  |
| recgL9cxHRTYz5IBf | `Testis_NAdj` | ORGAN | no | no | no |  |  |
| recoyrcDflVX1NEyL | `CTV1^GTV/Cervix/Uterus` | CTV | no | no | no |  | length |
| recyx3l3lVHQXNIXW | `CTV2^Parametria/Vagina` | CTV | no | no | no |  | length |
| recnYvLaWKJzYTc1H | `A_Carotid` | ORGAN | exact | exact | exact | 3939 |  |
| recT07eUgtwIyn1AV | `Scalp` | ORGAN | exact | exact | exact | 46494 |  |
| recIMxdjEHSKBD5hf | `Oropharynx` | ORGAN | exact | exact | exact | 54879 |  |
| reckSMFhMUz1tHqPx | `TM_Joint` | ORGAN | no | exact | exact | 54832 |  |
| recmiNPQsRFFMR0O2 | `Temporal_Lobe_L` | ORGAN | no | exact | exact | 72972 |  |
| rec5kwpaD4O7uJC6k | `Temporal_Lobe_R` | ORGAN | no | exact | exact | 72971 |  |
| rec8C9XSWdN3xtiG1 | `Retina_L` | ORGAN | exact | exact | exact | 58303 |  |
| rec1S5JB8wVPn5qnn | `Retina_R` | ORGAN | exact | exact | exact | 58302 |  |
| recgJAwaGVpeo3grK | `Ventricle_L` | ORGAN | exact | exact | exact | 7101 |  |
| rec01DbRnP8IAVRtF | `Bronchus_Main` | ORGAN | exact | exact | exact | 7405 |  |
| recpl2evFWtvtyUvq | `V_Venacava_I` | ORGAN | exact | exact | exact | 10951 |  |
| reclLBr64l8fAkRjJ | `BileDuct_Common` | ORGAN | exact | exact | exact | 14667 |  |
| recGsd45zxLeS70LD | `GallBladder` | ORGAN | case only | exact | exact | 7202 |  |
| recHpzbX9ePe26k7G | `Skin` | ORGAN | exact | exact | exact | 7163 |  |
| reciWPjWUSFwtwWDP | `Joint_Spc` | ORGAN | no | no | no |  |  |
| rece9WNfeuVDuw9ib | `Hi HU` | AVOIDANCE | no | no | no |  | no_spaces |
| recNADTZOwimZ35kd | `GTVsb` | GTV | no | no | no |  |  |
| recUoeSPJFYaQyQBR | `CTVsb` | CTV | no | no | no |  |  |
| rec2JbR83EOfyMp3U | `_TS_PTV` | PTV | no | no | no |  |  |
| rech6XWgeJxWgOCPy | `_TS_GTVp` | GTV | no | no | no |  |  |
| recSpZHzfVyMzWym2 | `_TS_GTVsb` | GTV | no | no | no |  |  |
| recAe5j0WXAwHkkZr | `_TS_CTVn` | CTV | no | no | no |  |  |
| recIsfMPsHNM4NhBw | `_TS_CTVp` | CTV | no | no | no |  |  |
| recBAais5daEWac4n | `_TS_CTVsb` | CTV | no | no | no |  | unique |
| recDweuv97sjjOLhV | `SpinalCanal` | ORGAN | exact | exact | exact | 9680 |  |
| rec2ZIW8LMU4nPGYR | `CTV_Mid01` | CTV | no | no | no |  |  |
| recJk6Lc5eGE2AHq1 | `CTV_Mid02` | CTV | no | no | no |  |  |
| recmrqHUats1hniIz | `PTV_Mid01` | PTV | no | no | no |  |  |
| recZK6TUxKj6D8B6P | `PTV_Mid02` | PTV | no | no | no |  |  |
| recfjOxYlBMnyAaIN | `CTV_ev12` | CTV | no | no | no |  |  |
| recscr3BP3ZVE9vhS | `PTV_Surf` | PTV | no | no | no |  |  |
| recvdR9nKp0jchjTw | `zPTVopt` | Control | no | no | no |  |  |
| recBQZYydhUesPUul | `PTV_Eval` | PTV | no | no | no |  |  |
| recQUTvuPbOsGSscY | `Appl_evXX` | BRACHY_SRC_APP | no | no | no |  |  |
| recF4dhtC2zWvDkfi | `Breast_R-PTV` | ORGAN | no | no | no |  |  |
| recUF4MFWiqrdgVzk | `Breast_R-CTV` | ORGAN | no | no | no |  |  |
| recBkY9oYGvuF0UlJ | `Breast_L-PTV` | ORGAN | no | no | no |  |  |
| rec1ADYzRNOCKjQSb | `Breast_L-CTV` | ORGAN | no | no | no |  |  |
| recNDXRC3aemcQ5JJ | `LN_Pelvic_R` | IRRAD_VOLUME | exact | exact | exact |  |  |
| recquY3COhiodnar5 | `LN_Pelvic_L` | IRRAD_VOLUME | exact | exact | exact |  |  |
| recNq0aBdHoBN3D6m | `Prostate_evXX` | ORGAN | no | no | no |  |  |
| recCsBYKEvpblX4c2 | `Femur_R` | ORGAN | exact | exact | exact | 24474 |  |
| reclfEvlHiegQOO1i | `Femur_L` | ORGAN | exact | exact | exact | 24475 |  |
| rec3qFyu0ttTMnGiU | `CTV1` | CTV | no | no | no |  |  |
| recb3kqfG7jlAEcWw | `CTV2` | CTV | no | no | no |  |  |
| recyXxk22gi7wIy1v | `LN_Pelvics` | IRRAD_VOLUME | exact | exact | exact |  |  |
| recug3voafkIxnk45 | `GTV1` | GTV | no | no | no |  |  |
| recjKSJiZjsAxNblE | `GTV2` | GTV | no | no | no |  |  |
| recsJ4dS7KzxuRrif | `CTV_Eval` | CTV | no | no | no |  |  |
| recrLlSnNV0d2ykNi | `PTV_ev10` | PTV | no | no | no |  |  |
| rec6FK2SAz3RToHYB | `Skin03` | ORGAN | no | no | no |  |  |
| recNzTYHqiu0Qm1Yu | `Appl_Surf` | BRACHY_SRC_APP | no | no | no |  |  |
| reccgjDBF5cUz6sUW | `PTV_Boost` | PTV | no | no | no |  |  |
| recxuv0R42c95N879 | `OpticDisc_L` | ORGAN | no | no | no |  |  |
| recAfmkVBO289LL0E | `OpticDisc_R` | ORGAN | no | no | no |  |  |
| recgCKR9NnyZvNQIb | `Choroid` | ORGAN | no | no | no |  |  |
| recOeyXj66KvEhiju | `Apex` | ORGAN | no | no | no |  |  |
| recT7zAoyJBR3HXVO | `Sclera_Inner` | ORGAN | no | no | no |  |  |
| rec73PrIbVFrbphye | `Sclera_Outer` | ORGAN | no | no | no |  |  |
| recE7cGqHwmnctZAf | `PTV_XXXX` | PTV | no | no | no |  |  |
| recSSBFQrw5MlLjdx | `Eye_Origin_R` | ORGAN | no | no | no |  |  |
| recRFZpTifBAtACFU | `Eye_Origin_L` | ORGAN | no | no | no |  |  |
| recYncs2k0vQL9hSD | `Fovea_L` | ORGAN | no | no | no |  |  |
| recnZRPn6BDAc2qsa | `Fovea_R` | ORGAN | no | no | no |  |  |
| rec4wER6CAFjbgsa8 | `GTV_LR` | GTV | no | no | no |  |  |
| recqOCJEZDXb5PnXr | `GTV_IR` | GTV | no | no | no |  |  |
| recI0UlgLIAeBNYQQ | `GTV_HR` | GTV | no | no | no |  |  |
| recaiQ28bUPLKCxlb | `CTV_LR` | CTV | no | no | no |  |  |
| recNwKx5QzZ5aHPJs | `CTV_IR` | CTV | no | no | no |  |  |
| recFkJqfURpw7Seh5 | `CTV_HR` | CTV | no | no | no |  |  |
| recSmZyTXudK4XhME | `PTV_LR` | PTV | no | no | no |  |  |
| recAb2twRT0zAqrVH | `PTV_IR` | PTV | no | no | no |  |  |
| recoqeZTyWWS0mKnX | `PTV_HR` | PTV | no | no | no |  |  |
| recS9pSwdxhz70Om9 | `Vagina_Surf` | ORGAN | exact | exact | exact |  |  |
| recpCO9GcDKPWMQEo | `Cervix` | ORGAN | exact | exact | exact | 17740 |  |
| recEzffrVpUjDmr5K | `SkinXX` | ORGAN | no | no | no |  |  |
| recy9f5hBfw1QhB44 | `SkinXX_PRVXX` | ORGAN | no | no | no |  |  |
