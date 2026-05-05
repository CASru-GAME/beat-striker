terraform {
  required_version = ">= 1.6.0"

  backend "gcs" {
    bucket = "tf-state-beat-495400"
    prefix = "terraform/state"
  }

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

resource "google_project_service" "services" {
  for_each = toset([
    "run.googleapis.com",
    "cloudbuild.googleapis.com",
    "firestore.googleapis.com",
    "artifactregistry.googleapis.com"
  ])

  project = var.project_id
  service = each.value

  disable_on_destroy = false
}

resource "google_service_account" "cloud_run" {
  account_id   = "beat-striker-run"
  display_name = "Beat Striker Cloud Run runtime"
}

resource "google_project_iam_member" "cloud_run_firestore" {
  project = var.project_id
  role    = "roles/datastore.user"
  member  = "serviceAccount:${google_service_account.cloud_run.email}"
}

resource "google_firestore_database" "default" {
  project     = var.project_id
  name        = "(default)"
  location_id = var.firestore_location
  type        = "FIRESTORE_NATIVE"

  depends_on = [
    google_project_service.services
  ]
}

resource "google_firestore_field" "presence_expires_at" {
  project    = var.project_id
  database   = google_firestore_database.default.name
  collection = "presence"
  field      = "expiresAt"

  ttl_config {}
}

resource "google_firestore_field" "invites_expires_at" {
  project    = var.project_id
  database   = google_firestore_database.default.name
  collection = "invites"
  field      = "expiresAt"

  ttl_config {}
}

resource "google_firestore_field" "reservations_expires_at" {
  project    = var.project_id
  database   = google_firestore_database.default.name
  collection = "reservations"
  field      = "expiresAt"

  ttl_config {}
}

resource "google_firestore_index" "presence_available_by_expiry" {
  project     = var.project_id
  database    = google_firestore_database.default.name
  collection  = "presence"
  query_scope = "COLLECTION"

  fields {
    field_path = "state"
    order      = "ASCENDING"
  }

  fields {
    field_path = "expiresAt"
    order      = "ASCENDING"
  }
}

resource "google_firestore_index" "invites_incoming_by_expiry" {
  project     = var.project_id
  database    = google_firestore_database.default.name
  collection  = "invites"
  query_scope = "COLLECTION"

  fields {
    field_path = "toSessionId"
    order      = "ASCENDING"
  }

  fields {
    field_path = "status"
    order      = "ASCENDING"
  }

  fields {
    field_path = "expiresAt"
    order      = "ASCENDING"
  }
}

resource "google_firestore_index" "reservations_active_by_player" {
  project     = var.project_id
  database    = google_firestore_database.default.name
  collection  = "reservations"
  query_scope = "COLLECTION"

  fields {
    field_path   = "playerSessionIds"
    array_config = "CONTAINS"
  }

  fields {
    field_path = "status"
    order      = "ASCENDING"
  }

  fields {
    field_path = "expiresAt"
    order      = "ASCENDING"
  }
}
